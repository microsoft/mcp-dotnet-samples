@description('The location used for all deployed resources')
param location string = resourceGroup().location

@description('Tags that will be applied to all resources')
param tags object = {}

@description('The name of the service defined in azure.yaml.')
param azdServiceName string

@description('Personal 365 Refresh Token for OneDrive access (will be set via postdeploy hook)')
@secure()
param personal365RefreshToken string = ''

var abbrs = loadJsonContent('./abbreviations.json')
var resourceToken = uniqueString(subscription().id, resourceGroup().id, location)
var functionAppName = '${abbrs.webSitesFunctions}${azdServiceName}-${resourceToken}'
var deploymentStorageContainerName = 'app-package-${take(functionAppName, 32)}-${take(toLower(uniqueString(functionAppName, resourceToken)), 7)}'

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

// API Management
module apimService './modules/apim.bicep' = {
  name: 'apimService'
  params:{
    apiManagementName: '${abbrs.apiManagementService}${resourceToken}'
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

  resource blobServices 'blobServices' = {
    name: 'default'
    resource container 'containers' = {
      name: deploymentStorageContainerName
      properties: {
        publicAccess: 'None'
      }
    }
  }
}

// 3. User-assigned managed identity for the function app
resource userAssignedIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: '${abbrs.managedIdentityUserAssignedIdentities}${azdServiceName}-${resourceToken}'
  location: location
  tags: tags
}

// 4. App Service plan (Flex Consumption)
resource appServicePlan 'Microsoft.Web/serverfarms@2022-09-01' = {
  name: '${abbrs.webServerFarms}${resourceToken}'
  location: location
  tags: tags
  sku: {
    name: 'FC1'
    tier: 'FlexConsumption'
  }
  properties: {
    reserved: true // Required for Linux
  }
}

// Storage account for File Share (Connection String support)
resource fileShareStorage 'Microsoft.Storage/storageAccounts@2022-09-01' = {
  name: '${abbrs.storageStorageAccounts}${resourceToken}files'
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

  resource fileServices 'fileServices' = {
    name: 'default'
    resource share 'shares' = {
      name: 'downloads'
      properties: {
        accessTier: 'TransactionOptimized'
        shareQuota: 100
      }
    }
  }
}

// 5. The Web App
module fncapp './modules/functionapp.bicep' = {
  name: 'functionapp'
  params: {
    name: functionAppName
    location: location
    tags: tags
    azdServiceName: azdServiceName
    applicationInsightsName: monitoring.outputs.applicationInsightsName
    appServicePlanId: appServicePlan.id
    runtimeName: 'dotnet-isolated'
    runtimeVersion: '9.0'
    storageAccountName: storageAccount.name
    deploymentStorageContainerName: deploymentStorageContainerName
    identityId: userAssignedIdentity.id
    identityClientId: userAssignedIdentity.properties.clientId
    appSettings: {
      OnedriveDownload__EntraId__UseManagedIdentity: 'true'
      OnedriveDownload__EntraId__TenantId: tenant().tenantId
      OnedriveDownload__EntraId__UserAssignedClientId: userAssignedIdentity.properties.clientId
      OnedriveDownload__EntraId__ClientId: entraApp.outputs.mcpAppId
      OnedriveDownload__EntraId__Personal365RefreshToken: personal365RefreshToken
      FileShareConnectionString: 'DefaultEndpointsProtocol=https;AccountName=${fileShareStorage.name};AccountKey=${fileShareStorage.listKeys().keys[0].value};EndpointSuffix=${environment().suffixes.storage}'
    }
  }
}

// MCP Entra App
module entraApp './modules/mcp-entra-app.bicep' = {
  name: 'mcpEntraApp'
  params: {
    mcpAppUniqueName: 'mcp-onedrivedownload-${resourceToken}'
    mcpAppDisplayName: 'MCP-OneDriveDownload-${resourceToken}'
    userAssignedIdentityPrincipleId: userAssignedIdentity.properties.principalId
    functionAppName: functionAppName
    appScopes: [
      'User.Read'
      'Files.Read.All'
      'offline_access'
    ]
    appRoles: []
  }
}

// MCP server API endpoints
module mcpApiModule './modules/mcp-api.bicep' = {
  name: 'mcpApiModule'
  params: {
    apimServiceName: apimService.outputs.name
    functionAppName: functionAppName
    mcpAppId: entraApp.outputs.mcpAppId
    mcpAppTenantId: entraApp.outputs.mcpAppTenantId
  }
  dependsOn: [
    fncapp
  ]
}

// Grant the function app's identity access to the storage account
var storageBlobDataOwnerRole = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'b7e6dc6d-f1e8-4753-8033-0f276bb0955b')

resource rbac 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storageAccount.id, userAssignedIdentity.id, storageBlobDataOwnerRole)
  scope: storageAccount
  properties: {
    principalId: userAssignedIdentity.properties.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: storageBlobDataOwnerRole
  }
}

// Grant the function app's identity access to the file share storage account
var storageFileDataSmbShareContributorRole = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '0c867c2a-1d8c-454a-a3db-ab2ea1bdc8bb')

resource fileShareStorageRbac 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(fileShareStorage.id, userAssignedIdentity.id, storageFileDataSmbShareContributorRole)
  scope: fileShareStorage
  properties: {
    principalId: userAssignedIdentity.properties.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: storageFileDataSmbShareContributorRole
  }
}


// Outputs for azd
output AZURE_RESOURCE_MCP_ONEDRIVE_DOWNLOAD_ID string = fncapp.outputs.resourceId
output AZURE_RESOURCE_MCP_ONEDRIVE_DOWNLOAD_NAME string = fncapp.outputs.name
output AZURE_RESOURCE_MCP_ONEDRIVE_DOWNLOAD_FQDN string = fncapp.outputs.fqdn
output AZURE_RESOURCE_MCP_ONEDRIVE_DOWNLOAD_GATEWAY_FQDN string = replace(apimService.outputs.gatewayUrl, 'https://', '')
output AZURE_USER_ASSIGNED_IDENTITY_PRINCIPAL_ID string = userAssignedIdentity.properties.principalId
output mcpAppId string = entraApp.outputs.mcpAppId
// This output is no longer relevant, but keeping it to avoid breaking main.bicep for now. I will fix main.bicep next.
output AZURE_CONTAINER_REGISTRY_ENDPOINT string = ''
