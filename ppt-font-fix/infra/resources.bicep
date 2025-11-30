@description('The location used for all deployed resources')
param location string = resourceGroup().location

@description('Tags that will be applied to all resources')
param tags object = {}

param mcpPptFontFixExists bool

@description('Id of the user or app to assign application roles')
param principalId string

var containerAppName = 'ppt-font-fix'

var abbrs = loadJsonContent('./abbreviations.json')
var resourceToken = uniqueString(subscription().id, resourceGroup().id, location)
var storageKey = storage.listKeys().keys[0].value
var storageConnectionString = 'DefaultEndpointsProtocol=https;AccountName=${storage.name};AccountKey=${storageKey};EndpointSuffix=core.windows.net'


// Monitor application with Azure Monitor
module monitoring 'br/public:avm/ptn/azd/monitoring:0.1.0' = {
  name: 'monitoring'
  params: {
    logAnalyticsName: '${abbrs.operationalInsightsWorkspaces}${resourceToken}'
    applicationInsightsName: '${abbrs.insightsComponents}${resourceToken}'
    applicationInsightsDashboardName: '${abbrs.portalDashboards}${resourceToken}'
    location: location
    tags: tags
  }
}

// Storage
resource storage 'Microsoft.Storage/storageAccounts@2022-09-01' = {
  name: 'st${resourceToken}'
  location: location
  kind: 'StorageV2'
  tags: tags
  sku: {
    name: 'Standard_LRS'
  }
  properties: {
    largeFileSharesState: 'Enabled'
  }
}

resource storageBlobService 'Microsoft.Storage/storageAccounts/blobServices@2022-09-01' = {
  parent: storage
  name: 'default'
}

resource generatedFilesContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2022-09-01' = {
  parent: storageBlobService
  name: 'generated-files'
  properties: {
    publicAccess: 'None'
  }
}

resource storageFileService 'Microsoft.Storage/storageAccounts/fileServices@2022-09-01' = {
  parent: storage
  name: 'default'
}

resource storageFileShare 'Microsoft.Storage/storageAccounts/fileServices/shares@2022-09-01' = {
  parent: storageFileService
  name: 'ppt-files'
  properties: {
    shareQuota: 1024
    enabledProtocols: 'SMB'
  }
}

// Container Registry
module containerRegistry 'br/public:avm/res/container-registry/registry:0.1.1' = {
  name: 'registry'
  params: {
    name: '${abbrs.containerRegistryRegistries}${resourceToken}'
    location: location
    tags: tags
    publicNetworkAccess: 'Enabled'
    roleAssignments: [
      {
        principalId: mcpPptFontFixIdentity.outputs.principalId
        principalType: 'ServicePrincipal'
        // ACR pull role
        roleDefinitionIdOrName: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '7f951dda-4ed3-4680-a7ca-43fe172d538d')
      }
    ]
  }
}

// Container apps environment
module containerAppsEnvironment 'br/public:avm/res/app/managed-environment:0.11.3' = {
  name: 'container-apps-environment'
  params: {
    appInsightsConnectionString: monitoring.outputs.applicationInsightsConnectionString
    name: '${abbrs.appManagedEnvironments}${resourceToken}'
    location: location
    zoneRedundant: false
    publicNetworkAccess: 'Enabled'
    
    workloadProfiles: [
      {
        workloadProfileType: 'Consumption'
        name: 'Consumption'
      }
    ]
  }
}

resource existingEnv 'Microsoft.App/managedEnvironments@2023-05-01' existing = {
  name: '${abbrs.appManagedEnvironments}${resourceToken}'
}

resource explicitStorageLink 'Microsoft.App/managedEnvironments/storages@2023-05-01' = {
  parent: existingEnv
  name: 'ppt-storage-link'
  properties: {
    azureFile: {
      accountName: storage.name
      shareName: storageFileShare.name
      accessMode: 'ReadWrite'
      accountKey: storage.listKeys().keys[0].value
    }
  }
  dependsOn: [
    containerAppsEnvironment
  ]
}

module mcpPptFontFixIdentity 'br/public:avm/res/managed-identity/user-assigned-identity:0.2.1' = {
  name: 'mcpPptFontFixIdentity'
  params: {
    name: '${abbrs.managedIdentityUserAssignedIdentities}mcpPptFontFix-${resourceToken}'
    location: location
  }
}

// Azure Container Apps
module mcpPptFontFixFetchLatestImage './modules/fetch-container-image.bicep' = {
  name: 'mcpPptFontFix-fetch-image'
  params: {
    exists: mcpPptFontFixExists
    name: 'ppt-font-fix'
  }
}

module mcpPptFontFix 'br/public:avm/res/app/container-app:0.8.0' = {
  name: 'mcpPptFontFix'
  
  dependsOn: [
    explicitStorageLink 
    storage
  ]

  params: {
    name: containerAppName
    ingressTargetPort: 8080
    scaleMinReplicas: 1
    scaleMaxReplicas: 10
    secrets: {
      secureList: [
      ]
    }
    volumes: [
      {
        name: 'ppt-files-volume'
        storageType: 'AzureFile'
        storageName: 'ppt-storage-link'
      }
    ]
    containers: [
      {
        image: mcpPptFontFixFetchLatestImage.outputs.?containers[?0].?image ?? 'mcr.microsoft.com/azuredocs/containerapps-helloworld:latest'
        name: 'main'
        resources: {
          cpu: json('0.5')
          memory: '1.0Gi'
        }
        env: [
          {
            name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
            value: monitoring.outputs.applicationInsightsConnectionString
          }
          {
            name: 'AZURE_CLIENT_ID'
            value: mcpPptFontFixIdentity.outputs.clientId
          }
          {
            name: 'PORT'
            value: '8080'
          }
          {
            name: 'AzureBlobConnectionString' 
            value: storageConnectionString
          }
        ]
        args: [ '--http' ]
        volumeMounts: [
          {
            volumeName: 'ppt-files-volume'
            mountPath: '/app/mounts'
          }
        ]
      }
    ]
    managedIdentities: {
      systemAssigned: false
      userAssignedResourceIds: [
        mcpPptFontFixIdentity.outputs.resourceId
      ]
    }
    registries: [
      {
        server: containerRegistry.outputs.loginServer
        identity: mcpPptFontFixIdentity.outputs.resourceId
      }
    ]
    corsPolicy: {
      allowedOrigins: [
        'https://make.preview.powerautomate.com'
        'https://make.preview.powerapps.com'
        'https://copilotstudio.preview.microsoft.com'
        'https://make.powerautomate.com'
        'https://make.powerapps.com'
        'https://copilotstudio.microsoft.com'
      ]
    }
    environmentResourceId: containerAppsEnvironment.outputs.resourceId
    location: location
    tags: union(tags, { 'azd-service-name': 'ppt-font-fix' })
  }
}
resource authConfig 'Microsoft.App/containerApps/authConfigs@2023-05-01' = {
  name: '${containerAppName}/current' 
  properties: {
    platform: {
      enabled: false 
    }
    globalValidation: {
      unauthenticatedClientAction: 'AllowAnonymous' 
    }
  }
  dependsOn: [
    mcpPptFontFix 
  ]
}

output AZURE_CONTAINER_REGISTRY_ENDPOINT string = containerRegistry.outputs.loginServer
output AZURE_RESOURCE_MCP_PPT_FONT_FIX_ID string = mcpPptFontFix.outputs.resourceId
output AZURE_RESOURCE_MCP_PPT_FONT_FIX_NAME string = mcpPptFontFix.outputs.name
output AZURE_RESOURCE_MCP_PPT_FONT_FIX_FQDN string = mcpPptFontFix.outputs.fqdn
output AZURE_STORAGE_ACCOUNT_NAME string = storage.name
output AZURE_FILE_SHARE_NAME string = storageFileShare.name
