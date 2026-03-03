// Main orchestration Bicep - deploys all modules
// Handles deployment order and conditional GenAI resources

param location string = 'uksouth'
param adminLogin string
param adminObjectId string
param deployGenAI bool = false

// Deploy App Service + Managed Identity
module appService 'app-service.bicep' = {
  name: 'appServiceDeployment'
  params: {
    location: location
  }
}

// Deploy Azure SQL
module azureSql 'azure-sql.bicep' = {
  name: 'azureSqlDeployment'
  params: {
    location: location
    adminLogin: adminLogin
    adminObjectId: adminObjectId
    managedIdentityPrincipalId: appService.outputs.managedIdentityPrincipalId
    managedIdentityName: appService.outputs.managedIdentityName
  }
}

// Conditionally deploy GenAI resources
module genAI 'genai.bicep' = if (deployGenAI) {
  name: 'genAIDeployment'
  params: {
    managedIdentityPrincipalId: appService.outputs.managedIdentityPrincipalId
  }
}

// Outputs
output sqlServerFqdn string = azureSql.outputs.sqlServerFqdn
output webAppName string = appService.outputs.webAppName
output webAppHostname string = appService.outputs.webAppHostname
output managedIdentityClientId string = appService.outputs.managedIdentityClientId
output managedIdentityPrincipalId string = appService.outputs.managedIdentityPrincipalId
output managedIdentityName string = appService.outputs.managedIdentityName

// Null-safe GenAI outputs (only populated when deployGenAI=true)
output openAIEndpoint string = deployGenAI ? genAI.outputs.openAIEndpoint : ''
output openAIModelName string = deployGenAI ? genAI.outputs.openAIModelName : ''
output openAIName string = deployGenAI ? genAI.outputs.openAIName : ''
output searchEndpoint string = deployGenAI ? genAI.outputs.searchEndpoint : ''
