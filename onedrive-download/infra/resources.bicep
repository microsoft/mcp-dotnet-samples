@description('The location used for all deployed resources')
param location string = resourceGroup().location

@description('Tags that will be applied to all resources')
param tags object = {}

@description('The name of the service defined in azure.yaml.')
param azdServiceName string

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
      OnedriveDownload__EntraId__TenantId: entraApp.outputs.mcpAppTenantId
      OnedriveDownload__EntraId__UserAssignedClientId: userAssignedIdentity.properties.clientId
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
      'Sites.Read.All'
    ]
    appRoles: []
  }
}

// Microsoft Graph app ID
var msGraphAppId = '00000003-0000-0000-c000-000000000000'

// Get Microsoft Graph service principal for role assignment
resource msGraphSP 'Microsoft.Graph/servicePrincipals@v1.0' existing = {
  appId: msGraphAppId
}

// Permission IDs needed for OneDrive file reading
var filesReadAllRoleId = '01ce198f-1ce1-47b3-a953-17dfad7d91e6' // Files.Read.All (delegated)
var sitesReadAllRoleId = '19dbc78e-2b68-4e8b-a46f-ab2ofc1dd4c7' // Sites.Read.All (delegated)

// Get the MCP Entra App service principal (created by entraApp module)
resource mcpEntraAppServicePrincipal 'Microsoft.Graph/servicePrincipals@v1.0' existing = {
  appId: entraApp.outputs.mcpAppId
}

// Grant Files.Read.All role to MCP app service principal
resource mcpAppFilesReadGrant 'Microsoft.Graph/appRoleAssignedTo@v1.0' = {
  resourceId: msGraphSP.id
  appRoleId: filesReadAllRoleId
  principalId: mcpEntraAppServicePrincipal.id
}

// Grant Files.Read.All role to user-assigned identity
resource userAssignedFilesReadGrant 'Microsoft.Graph/appRoleAssignedTo@v1.0' = {
  resourceId: msGraphSP.id
  appRoleId: filesReadAllRoleId
  principalId: userAssignedIdentity.properties.principalId
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
    mcpAppFilesReadGrant
    userAssignedFilesReadGrant
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

// Storage account for file shares
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

  resource fileService 'fileServices' = {
    name: 'default'
    resource fileShare 'shares' = {
      name: 'downloads'
      properties: {
        shareQuota: 1024 // 1 GiB
      }
    }
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
