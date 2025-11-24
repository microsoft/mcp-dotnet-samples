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
    largeFileSharesState: 'Enabled'
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

  // ★ File Share 추가: downloads 폴더를 마운트 가능하게 함
  resource fileServices 'fileServices' = {
    name: 'default'
    resource share 'shares' = {
      name: 'downloads'
      properties: {
        shareQuota: 1024
        enabledProtocols: 'SMB'
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

// 5. The Function App (직접 정의 - 스토리지 마운트 포함)
resource functionApp 'Microsoft.Web/sites@2023-12-01' = {
  name: functionAppName
  location: location
  kind: 'functionapp,linux'
  tags: union(tags, { 'azd-service-name': azdServiceName })
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${userAssignedIdentity.id}': {}
    }
  }
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true

    // ★★★ 핵심: 스토리지 마운트 설정 ★★★
    azureStorageAccounts: {
      'downloads-mount': {
        type: 'AzureFiles'
        accountName: storageAccount.name
        shareName: 'downloads'
        mountPath: '/home/mounts/downloads'
        accessKey: storageAccount.listKeys().keys[0].value
      }
    }

    siteConfig: {
      linuxFxVersion: 'DOTNET-ISOLATED|9.0'
      appSettings: [
        {
          name: 'AzureWebJobsStorage'
          value: 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};AccountKey=${storageAccount.listKeys().keys[0].value};EndpointSuffix=${environment().suffixes.storage}'
        }
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: monitoring.outputs.applicationInsightsConnectionString
        }
        {
          name: 'ApplicationInsightsAgent_EXTENSION_VERSION'
          value: '~3'
        }
        {
          name: 'XDT_MicrosoftApplicationInsights_Mode'
          value: 'recommended'
        }
        // ★ 다운로드 경로 설정 (마운트된 경로와 일치)
        {
          name: 'DOWNLOAD_DIR'
          value: '/home/mounts/downloads'
        }
        // ★ OneDrive 인증 관련 설정
        {
          name: 'OnedriveDownload__EntraId__UseManagedIdentity'
          value: 'true'
        }
        {
          name: 'OnedriveDownload__EntraId__TenantId'
          value: tenant().tenantId
        }
        {
          name: 'OnedriveDownload__EntraId__UserAssignedClientId'
          value: userAssignedIdentity.properties.clientId
        }
        {
          name: 'OnedriveDownload__EntraId__ClientId'
          value: entraApp.outputs.mcpAppId
        }
        {
          name: 'OnedriveDownload__EntraId__Personal365RefreshToken'
          value: personal365RefreshToken
        }
        {
          name: 'PERSONAL_365_REFRESH_TOKEN'
          value: personal365RefreshToken
        }
        {
          name: 'AZURE_CLIENT_ID'
          value: userAssignedIdentity.properties.clientId
        }
        {
          name: 'AZURE_STORAGE_CONNECTION_STRING'
          value: 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};AccountKey=${storageAccount.listKeys().keys[0].value};EndpointSuffix=${environment().suffixes.storage}'
        }
      ]
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
    functionApp
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

// Outputs for azd
output AZURE_RESOURCE_MCP_ONEDRIVE_DOWNLOAD_ID string = functionApp.id
output AZURE_RESOURCE_MCP_ONEDRIVE_DOWNLOAD_NAME string = functionApp.name
output AZURE_RESOURCE_MCP_ONEDRIVE_DOWNLOAD_FQDN string = functionApp.properties.defaultHostName
output AZURE_RESOURCE_MCP_ONEDRIVE_DOWNLOAD_GATEWAY_FQDN string = replace(apimService.outputs.gatewayUrl, 'https://', '')
output AZURE_USER_ASSIGNED_IDENTITY_PRINCIPAL_ID string = userAssignedIdentity.properties.principalId
output mcpAppId string = entraApp.outputs.mcpAppId
// This output is no longer relevant, but keeping it to avoid breaking main.bicep for now. I will fix main.bicep next.
output AZURE_CONTAINER_REGISTRY_ENDPOINT string = ''
output AZURE_STORAGE_CONNECTION_STRING string = 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};AccountKey=${storageAccount.listKeys().keys[0].value};EndpointSuffix=${environment().suffixes.storage}'
