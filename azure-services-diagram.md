# Azure Services Diagram

This diagram shows all Azure services created by this repository and how they connect.

```mermaid
graph TD
    User["👤 User / Browser"] -->|HTTPS| AppService["App Service\napp-expensemgmt-[suffix]\nUKSOUTH · S1 Standard"]
    User -->|Chat UI| ChatPage["Chat Page /Chat\n(in App Service)"]
    User -->|API Docs| Swagger["Swagger UI /swagger\n(in App Service)"]

    AppService -->|Managed Identity Auth\nActive Directory Managed Identity| AzureSQL["Azure SQL Database\nNorthwind\nBasic Tier · UKSOUTH"]
    AppService -->|Calls via REST API| AppService

    ChatPage -->|Azure.AI.OpenAI SDK\nManagedIdentityCredential| AzureOpenAI["Azure OpenAI\ngpt-4o model\nswedencentral · S0"]
    ChatPage -->|Function Calling API| AppService

    ManagedIdentity["User-Assigned\nManaged Identity\nmid-appmodassist-[suffix]"] -->|Assigned to| AppService
    ManagedIdentity -->|db_datareader/writer\n+ EXECUTE| AzureSQL
    ManagedIdentity -->|Cognitive Services\nOpenAI User role| AzureOpenAI
    ManagedIdentity -->|Search Index\nContributor role| AISearch["Azure AI Search\nsrch-expensemgmt-[suffix]\nBasic SKU · UKSOUTH"]

    AzureOpenAI -->|RAG pattern| AISearch

    subgraph "Resource Group: rg-expensemgmt-demo (UKSOUTH)"
        AppService
        AzureSQL
        ManagedIdentity
        AISearch
        AppServicePlan["App Service Plan\nplan-expensemgmt-[suffix]\nS1 Standard"]
        AppServicePlan --> AppService
    end

    subgraph "Sweden Central (GenAI quota)"
        AzureOpenAI
    end

    subgraph "Deployment Pipeline"
        DeployScript["deploy.sh / deploy-with-chat.sh"] -->|az deployment group create| ResourceGroup["Resource Group"]
        DeployScript -->|python3 run-sql.py| AzureSQL
        DeployScript -->|python3 run-sql-dbrole.py| AzureSQL
        DeployScript -->|python3 run-sql-stored-procs.py| AzureSQL
        DeployScript -->|az webapp deploy app.zip| AppService
    end
```

## Services Summary

| Service | Name Pattern | Region | SKU | Purpose |
|---------|-------------|--------|-----|---------|
| App Service Plan | `plan-expensemgmt-[suffix]` | UKSOUTH | S1 Standard | Hosts the web app |
| App Service | `app-expensemgmt-[suffix]` | UKSOUTH | S1 Standard | ASP.NET Razor Pages + APIs |
| Azure SQL Server | `sql-expensemgmt-[suffix]` | UKSOUTH | - | SQL Server (Entra ID only) |
| Azure SQL Database | `Northwind` | UKSOUTH | Basic | Expense data |
| User-Assigned MI | `mid-appmodassist-[suffix]` | UKSOUTH | - | Authentication identity |
| Azure OpenAI | `oai-expensemgmt-[suffix]` | Sweden Central | S0 | GPT-4o for chat |
| Azure AI Search | `srch-expensemgmt-[suffix]` | UKSOUTH | Basic | RAG knowledge base |

> **Note:** Azure OpenAI is deployed to **Sweden Central** even when the resource group is in UKSOUTH, to ensure GPT-4o model quota availability.
