@description('The location used for all deployed resources')
param location string = resourceGroup().location

@description('Tags that will be applied to all resources')
param tags object = {}

@description('The name of the service defined in azure.yaml.')
param azdServiceName string

var abbrs = loadJsonContent('./abbreviations.json')
var resourceToken = uniqueString(subscription().id, resourceGroup().id, location)

// 1. Monitoring
module monitoring 'br/public:avm/ptn/azd/monitoring:0.1.0' = {
  name: 'monitoring'
  params: {
    logAnalyticsName: '${abbrs.operationalInsightsWorkspaces}${resourceToken}'
    applicationInsightsName: '${abbrs.insightsComponents}${resourceToken}'
    location: location
    tags: tags
  }
}

// 2. Storage account for the function app
resource storageAccount 'Microsoft.Storage/storageAccounts@2022-09-01' = {
  name: '${abbrs.storageStorageAccounts}${resourceToken}'
  location: location
  tags: tags
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    minimumTlsVersion: 'TLS1_2'
    supportsHttpsTrafficOnly: true
    allowBlobPublicAccess: false
  }
}

// 3. User-assigned managed identity for the function app
resource userAssignedIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: '${abbrs.managedIdentityUserAssignedIdentities}${azdServiceName}-${resourceToken}'
  location: location
  tags: tags
}

// 4. App Service plan (Serverless consumption)
resource appServicePlan 'Microsoft.Web/serverfarms@2022-09-01' = {
  name: '${abbrs.webServerFarms}${resourceToken}'
  location: location
  tags: tags
  sku: {
    name: 'Y1'
    tier: 'Dynamic'
  }
  properties: {
    reserved: true // Required for Linux consumption plan
  }
}

// 5. The Function App
resource functionApp 'Microsoft.Web/sites@2022-09-01' = {
  name: '${abbrs.webSitesFunctions}${azdServiceName}-${resourceToken}'
  location: location
  tags: union(tags, { 'azd-service-name': azdServiceName })
  kind: 'functionapp,linux'
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${userAssignedIdentity.id}': {}
    }
  }
  properties: {
    serverFarmId: appServicePlan.id
    siteConfig: {
      appSettings: [
        {
          name: 'AzureWebJobsStorage__accountName'
          value: storageAccount.name
        }
        {
          name: 'FUNCTIONS_WORKER_RUNTIME'
          value: 'custom' // For custom handlers
        }
        {
          name: 'FUNCTIONS_EXTENSION_VERSION'
          value: '~4'
        }
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: monitoring.outputs.applicationInsightsConnectionString
        }
        {
          name: 'OnedriveDownload__EntraId__UseManagedIdentity'
          value: 'true'
        }
        {
          name: 'OnedriveDownload__EntraId__UserAssignedClientId'
          value: userAssignedIdentity.properties.clientId
        }
      ]
      cors: {
        allowedOrigins: [
          'https://portal.azure.com'
        ]
      }
      linuxFxVersion: 'DOTNET-ISOLATED|8.0' // Even for custom handlers, a base runtime is needed.
    }
    httpsOnly: true
    clientAffinityEnabled: false
  }
}

// Grant the function app's identity access to the storage account
var storageBlobDataOwnerRole = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'b7e6dc6d-f1e8-4753-8033-0f276bb0955b')

resource rbac 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(functionApp.id, storageAccount.id, storageBlobDataOwnerRole)
  scope: storageAccount
  properties: {
    principalId: userAssignedIdentity.properties.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: storageBlobDataOwnerRole
  }
}

// Outputs for azd
output AZURE_RESOURCE_MCP_ONEDRIVE_DOWNLOAD_ID string = functionApp.id
output AZURE_RESOURCE_MCP_ONEDRIVE_DOWNLOAD_NAME string = functionApp.name
output AZURE_RESOURCE_MCP_ONEDRIVE_DOWNLOAD_FQDN string = functionApp.properties.defaultHostName
// This output is no longer relevant, but keeping it to avoid breaking main.bicep for now. I will fix main.bicep next.
output AZURE_CONTAINER_REGISTRY_ENDPOINT string = ''
