#!/bin/bash
# ============================================================
# Deploy Expense Manager App WITH GenAI/Chat services
# One-line deploy: RESOURCE_GROUP=rg-myapp LOCATION=uksouth ADMIN_LOGIN=me@org.com ADMIN_OBJECT_ID=<your-aad-object-id> bash deploy-with-chat.sh
# ============================================================
set -euo pipefail

RESOURCE_GROUP="${RESOURCE_GROUP:-rg-expensemgmt-demo}"
LOCATION="${LOCATION:-uksouth}"
ADMIN_LOGIN="${ADMIN_LOGIN:-admin@example.com}"
ADMIN_OBJECT_ID="${ADMIN_OBJECT_ID:-00000000-0000-0000-0000-000000000000}"

echo "============================================"
echo " Expense Manager - Deploy WITH GenAI Chat"
echo " Resource Group : $RESOURCE_GROUP"
echo " Location       : $LOCATION"
echo "============================================"

# ── Step 1: Deploy Resource Group + Full Infrastructure (incl. GenAI) ───────
echo ""
echo "[1/11] Deploying resource group and infrastructure (including GenAI)..."
az group create --name "$RESOURCE_GROUP" --location "$LOCATION" --output none

DEPLOYMENT_OUTPUT=$(az deployment group create \
    --resource-group "$RESOURCE_GROUP" \
    --template-file infra/main.bicep \
    --parameters adminLogin="$ADMIN_LOGIN" adminObjectId="$ADMIN_OBJECT_ID" deployGenAI=true \
    --query "properties.outputs" \
    --output json)

echo "Deployment outputs:"
echo "$DEPLOYMENT_OUTPUT" | python3 -c "import sys,json; d=json.load(sys.stdin); [print(f'  {k}: {v[\"value\"]}') for k,v in d.items()]"

SQL_SERVER_FQDN=$(echo "$DEPLOYMENT_OUTPUT" | python3 -c "import sys,json; print(json.load(sys.stdin)['sqlServerFqdn']['value'])")
WEB_APP_NAME=$(echo "$DEPLOYMENT_OUTPUT" | python3 -c "import sys,json; print(json.load(sys.stdin)['webAppName']['value'])")
MANAGED_IDENTITY_CLIENT_ID=$(echo "$DEPLOYMENT_OUTPUT" | python3 -c "import sys,json; print(json.load(sys.stdin)['managedIdentityClientId']['value'])")
MANAGED_IDENTITY_NAME=$(echo "$DEPLOYMENT_OUTPUT" | python3 -c "import sys,json; print(json.load(sys.stdin)['managedIdentityName']['value'])")
OPENAI_ENDPOINT=$(echo "$DEPLOYMENT_OUTPUT" | python3 -c "import sys,json; print(json.load(sys.stdin).get('openAIEndpoint',{}).get('value',''))")
OPENAI_MODEL_NAME=$(echo "$DEPLOYMENT_OUTPUT" | python3 -c "import sys,json; print(json.load(sys.stdin).get('openAIModelName',{}).get('value','gpt-4o'))")
SEARCH_ENDPOINT=$(echo "$DEPLOYMENT_OUTPUT" | python3 -c "import sys,json; print(json.load(sys.stdin).get('searchEndpoint',{}).get('value',''))")

echo ""
echo "  SQL Server FQDN         : $SQL_SERVER_FQDN"
echo "  Web App Name            : $WEB_APP_NAME"
echo "  Managed Identity Client : $MANAGED_IDENTITY_CLIENT_ID"
echo "  OpenAI Endpoint         : $OPENAI_ENDPOINT"
echo "  Search Endpoint         : $SEARCH_ENDPOINT"

# ── Step 2: Configure App Service settings ──────────────────────────────────
echo ""
echo "[2/11] Configuring App Service settings..."
CONNECTION_STRING="Server=tcp:${SQL_SERVER_FQDN};Database=Northwind;Authentication=Active Directory Managed Identity;User Id=${MANAGED_IDENTITY_CLIENT_ID};"

az webapp config connection-string set \
    --resource-group "$RESOURCE_GROUP" \
    --name "$WEB_APP_NAME" \
    --settings DefaultConnection="$CONNECTION_STRING" \
    --connection-string-type SQLAzure \
    --output none

az webapp config appsettings set \
    --resource-group "$RESOURCE_GROUP" \
    --name "$WEB_APP_NAME" \
    --settings \
        "AZURE_CLIENT_ID=$MANAGED_IDENTITY_CLIENT_ID" \
        "ManagedIdentityClientId=$MANAGED_IDENTITY_CLIENT_ID" \
        "OpenAI__Endpoint=$OPENAI_ENDPOINT" \
        "OpenAI__DeploymentName=$OPENAI_MODEL_NAME" \
        "SearchEndpoint=$SEARCH_ENDPOINT" \
    --output none

echo "  App Service settings configured (including GenAI)."

# ── Step 3: Wait for SQL Server ─────────────────────────────────────────────
echo ""
echo "[3/11] Waiting 30 seconds for SQL Server to be ready..."
sleep 30

# ── Step 4: SQL Firewall rules ───────────────────────────────────────────────
echo ""
echo "[4/11] Configuring SQL firewall rules..."
MY_IP=$(curl -s https://api.ipify.org)
SQL_SERVER_NAME=$(echo "$SQL_SERVER_FQDN" | cut -d'.' -f1)

az sql server firewall-rule create \
    --resource-group "$RESOURCE_GROUP" \
    --server "$SQL_SERVER_NAME" \
    --name "AllowAllAzureIPs" \
    --start-ip-address 0.0.0.0 \
    --end-ip-address 0.0.0.0 \
    --output none

az sql server firewall-rule create \
    --resource-group "$RESOURCE_GROUP" \
    --server "$SQL_SERVER_NAME" \
    --name "AllowDeploymentIP" \
    --start-ip-address "$MY_IP" \
    --end-ip-address "$MY_IP" \
    --output none

echo "  Firewall rules set (Azure services + current IP: $MY_IP)"
echo "  Waiting additional 15 seconds for firewall rules to propagate..."
sleep 15

# ── Step 5: Install Python dependencies ─────────────────────────────────────
echo ""
echo "[5/11] Installing Python dependencies..."
pip3 install --quiet pyodbc azure-identity

# Update Python scripts with actual server/database
sed -i.bak "s|<your-server>.database.windows.net|${SQL_SERVER_FQDN}|g" run-sql.py && rm -f run-sql.py.bak
sed -i.bak "s|<your-server>.database.windows.net|${SQL_SERVER_FQDN}|g" run-sql-dbrole.py && rm -f run-sql-dbrole.py.bak
sed -i.bak "s|<your-server>.database.windows.net|${SQL_SERVER_FQDN}|g" run-sql-stored-procs.py && rm -f run-sql-stored-procs.py.bak

# ── Step 6: Import DB schema ──────────────────────────────────────────────────
echo ""
echo "[6/11] Importing database schema..."
python3 run-sql.py

# ── Step 7: Configure DB roles for managed identity ─────────────────────────
echo ""
echo "[7/11] Configuring database roles for managed identity..."
sed -i.bak "s/MANAGED-IDENTITY-NAME/${MANAGED_IDENTITY_NAME}/g" script.sql && rm -f script.sql.bak
python3 run-sql-dbrole.py

# ── Step 8: Deploy stored procedures ────────────────────────────────────────
echo ""
echo "[8/11] Deploying stored procedures..."
python3 run-sql-stored-procs.py

# ── Step 9: Build and create app.zip ────────────────────────────────────────
echo ""
echo "[9/11] Building and packaging application..."
cd app && dotnet publish -c Release -o ./publish --nologo -v quiet
cd publish && zip -r ../../app.zip . -q && cd ../..
echo "  app.zip created."

# ── Step 10: Deploy application ──────────────────────────────────────────────
echo ""
echo "[10/11] Deploying application to App Service..."
az webapp deploy \
    --resource-group "$RESOURCE_GROUP" \
    --name "$WEB_APP_NAME" \
    --src-path ./app.zip \
    --type zip \
    --output none

# ── Step 11: Restart app ─────────────────────────────────────────────────────
echo ""
echo "[11/11] Restarting App Service to pick up new settings..."
az webapp restart --resource-group "$RESOURCE_GROUP" --name "$WEB_APP_NAME" --output none

echo ""
echo "============================================"
echo " ✅ Deployment Complete (with GenAI Chat)!"
echo "============================================"
echo ""
echo " App URL  : https://${WEB_APP_NAME}.azurewebsites.net/Index"
echo " Chat UI  : https://${WEB_APP_NAME}.azurewebsites.net/Chat"
echo " API Docs : https://${WEB_APP_NAME}.azurewebsites.net/swagger"
echo ""
echo " NOTE: Navigate to /Index to see the app dashboard."
echo "       The chat assistant at /Chat will now use real AI."
echo ""
