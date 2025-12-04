@description('The location used for all deployed resources')
param location string = resourceGroup().location

@description('Tags that will be applied to all resources')
param tags object = {}

param mcpOpenApiToSdkExists bool

@description('Id of the user or app to assign application roles')
param principalId string

var abbrs = loadJsonContent('./abbreviations.json')
var resourceToken = uniqueString(subscription().id, resourceGroup().id, location)

// Storage account and file share for the workspace
resource storage 'Microsoft.Storage/storageAccounts@2025-01-01' = {
  name: '${abbrs.storageStorageAccounts}${resourceToken}'
  location: location
  tags: tags
  kind: 'StorageV2'
  sku: {
    name: 'Standard_LRS'
  }
  properties: {
    minimumTlsVersion: 'TLS1_2'
    allowBlobPublicAccess: false
    publicNetworkAccess: 'Enabled'
  }
}

resource storageFileService 'Microsoft.Storage/storageAccounts/fileServices@2025-01-01' = {
  parent: storage
  name: 'default'
}

resource storageFileShare 'Microsoft.Storage/storageAccounts/fileServices/shares@2025-01-01' = {
  parent: storageFileService
  name: 'workspace'
  properties: {
    accessTier: 'TransactionOptimized'
    shareQuota: 1024
    enabledProtocols: 'SMB'
  }
}

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

// Container registry
module containerRegistry 'br/public:avm/res/container-registry/registry:0.1.1' = {
  name: 'registry'
  params: {
    name: '${abbrs.containerRegistryRegistries}${resourceToken}'
    location: location
    tags: tags
    publicNetworkAccess: 'Enabled'
    roleAssignments: [
      {
        principalId: mcpOpenApiToSdkIdentity.outputs.principalId
        principalType: 'ServicePrincipal'
        // ACR pull role
        roleDefinitionIdOrName: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '7f951dda-4ed3-4680-a7ca-43fe172d538d')
      }
    ]
  }
}

// Container apps environment
module containerAppsEnvironment 'br/public:avm/res/app/managed-environment:0.4.5' = {
  name: 'container-apps-environment'
  params: {
    logAnalyticsWorkspaceResourceId: monitoring.outputs.logAnalyticsWorkspaceResourceId
    name: '${abbrs.appManagedEnvironments}${resourceToken}'
    location: location
    zoneRedundant: false
  }
}

// Link storage to the container apps environment
resource env 'Microsoft.App/managedEnvironments@2025-01-01' existing = {
  name: '${abbrs.appManagedEnvironments}${resourceToken}'
}

resource envStorage 'Microsoft.App/managedEnvironments/storages@2025-01-01' = {
  parent: env
  name: 'workspace'
  properties: {
    azureFile: {
      accountName: storage.name
      accountKey: storage.listKeys().keys[0].value
      shareName: storageFileShare.name
      accessMode: 'ReadWrite'
    }
  }
  dependsOn: [
    containerAppsEnvironment
  ]
}

// User assigned identity
module mcpOpenApiToSdkIdentity 'br/public:avm/res/managed-identity/user-assigned-identity:0.2.1' = {
  name: 'mcpOpenApiToSdkIdentity'
  params: {
    name: '${abbrs.managedIdentityUserAssignedIdentities}mcp-openapi-to-sdk-${resourceToken}'
    location: location
  }
}

// Azure Container Apps - Image Fetching
module mcpOpenApiToSdkFetchLatestImage './modules/fetch-container-image.bicep' = {
  name: 'mcpOpenApiToSdkFetchLatestImage'
  params: {
    exists: mcpOpenApiToSdkExists
    name: 'openapi-to-sdk'
  }
}

// Azure Container Apps - Main App
module mcpOpenApiToSdk 'br/public:avm/res/app/container-app:0.8.0' = {
  name: 'mcpOpenApiToSdk'
  params: {
    name: 'openapi-to-sdk'
    ingressTargetPort: 8080
    scaleMinReplicas: 1
    scaleMaxReplicas: 10
    secrets: {
      secureList: [
      ]
    }
    
    // Define the volume using the envStorage created above
    volumes: [
      {
        name: 'workspace-vol'
        storageType: 'AzureFile'
        storageName: 'workspace'
      }
    ]

    containers: [
      {
        image: mcpOpenApiToSdkFetchLatestImage.outputs.?containers[?0].?image ?? 'mcr.microsoft.com/azuredocs/containerapps-helloworld:latest'
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
            value: mcpOpenApiToSdkIdentity.outputs.clientId
          }
          {
            name: 'PORT'
            value: '8080'
          }
        ]
        args: [
          '--http'
        ]
        
        // Mount the volume to a path inside the container
        volumeMounts: [
          {
            volumeName: 'workspace-vol'
            mountPath: '/app/workspace'
          }
        ]
      }
    ]
    managedIdentities: {
      systemAssigned: false
      userAssignedResourceIds: [
        mcpOpenApiToSdkIdentity.outputs.resourceId
      ]
    }
    registries: [
      {
        server: containerRegistry.outputs.loginServer
        identity: mcpOpenApiToSdkIdentity.outputs.resourceId
      }
    ]
    environmentResourceId: containerAppsEnvironment.outputs.resourceId
    corsPolicy: {
      allowedOrigins: [
        'https://make.preview.powerapps.com'
        'https://make.powerapps.com'
        'https://make.preview.powerautomate.com'
        'https://make.powerautomate.com'
        'https://copilotstudio.preview.microsoft.com'
        'https://copilotstudio.microsoft.com'
      ]
    }
    location: location
    tags: union(tags, { 'azd-service-name': 'openapi-to-sdk' })
  }
}

output AZURE_CONTAINER_REGISTRY_ENDPOINT string = containerRegistry.outputs.loginServer
output AZURE_RESOURCE_MCP_OPENAPI_TO_SDK_ID string = mcpOpenApiToSdk.outputs.resourceId
output AZURE_RESOURCE_MCP_OPENAPI_TO_SDK_NAME string = mcpOpenApiToSdk.outputs.name
output AZURE_RESOURCE_MCP_OPENAPI_TO_SDK_FQDN string = mcpOpenApiToSdk.outputs.fqdn
output AZURE_STORAGE_ACCOUNT_NAME string = storage.name
output AZURE_FILE_SHARE_NAME string = storageFileShare.name