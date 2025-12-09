@description('Azure region where resources will be deployed')
param location string = resourceGroup().location

@description('Tags applied to all resources')
param tags object = {}

param pptTranslatorExists bool

@description('Id of the user or app to assign application roles')
param principalId string

@secure()
param openAiApiKey string

var abbrs = loadJsonContent('./abbreviations.json')
var resourceToken = uniqueString(subscription().id, resourceGroup().id, location)
var pptTranslatorAppName = 'ppt-translator'

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
        principalId: pptTranslatorIdentity.outputs.principalId
        principalType: 'ServicePrincipal'
        // ACR pull role
        roleDefinitionIdOrName: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '7f951dda-4ed3-4680-a7ca-43fe172d538d')
      }
    ]
  }
}

// User assigned identity
module pptTranslatorIdentity 'br/public:avm/res/managed-identity/user-assigned-identity:0.2.1' = {
  name: 'pptTranslatorIdentity'
  params: {
    name: '${abbrs.managedIdentityUserAssignedIdentities}ppttrans-${resourceToken}'
    location: location
  }
}

//
// 1. Storage Account with Single File Share
//
resource storageAccount 'Microsoft.Storage/storageAccounts@2022-09-01' = {
  name: 'st${resourceToken}'
  location: location
  tags: tags
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    minimumTlsVersion: 'TLS1_2'
    allowBlobPublicAccess: false
    supportsHttpsTrafficOnly: true
  }

  resource fileServices 'fileServices' = {
    name: 'default'
    
    resource filesShare 'shares' = {
      name: 'ppt-files'
      properties: {
        shareQuota: 100  // 100GB
        enabledProtocols: 'SMB'
      }
    }
  }
}

//
// 2. Container App Environment
//
module containerAppsEnvironment 'br/public:avm/res/app/managed-environment:0.4.5' = {
  name: 'container-apps-environment'
  params: {
    logAnalyticsWorkspaceResourceId: monitoring.outputs.logAnalyticsWorkspaceResourceId
    name: '${abbrs.appManagedEnvironments}${resourceToken}'
    location: location
    zoneRedundant: false
  }
}

// Register Azure Files storage with Container App Environment
resource managedEnv 'Microsoft.App/managedEnvironments@2023-05-01' existing = {
  name: '${abbrs.appManagedEnvironments}${resourceToken}'
}

resource filesStorage 'Microsoft.App/managedEnvironments/storages@2023-05-01' = {
  name: 'files-storage'
  parent: managedEnv
  properties: {
    azureFile: {
      accountName: storageAccount.name
      accountKey: storageAccount.listKeys().keys[0].value
      shareName: storageAccount::fileServices::filesShare.name
      accessMode: 'ReadWrite'
    }
  }
}

//
// 3. Container App with Volume Mount
//
//
// 3. Container App with Volume Mount (using AVM module)
//
module pptTranslatorFetchLatestImage './modules/fetch-container-image.bicep' = {
  name: 'pptTranslator-fetch-image'
  params: {
    exists: pptTranslatorExists
    name: 'ppt-translator'
  }
}

module pptTranslator 'br/public:avm/res/app/container-app:0.8.0' = {
  name: 'pptTranslator'
  params: {
    name: pptTranslatorAppName
    location: location
    tags: union(tags, { 'azd-service-name': 'ppt-translator' })
    environmentResourceId: containerAppsEnvironment.outputs.resourceId
    ingressTargetPort: 8080
    scaleMinReplicas: 1
    scaleMaxReplicas: 10
    secrets: {
      secureList: [
        {
          name: 'openai-api-key'
          value: openAiApiKey
        }
        {
          name: 'storage-connection'
          value: 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};AccountKey=${storageAccount.listKeys().keys[0].value};EndpointSuffix=${environment().suffixes.storage}'
        }
      ]
    }
    containers: [
      {
        image: pptTranslatorFetchLatestImage.outputs.?containers[?0].?image ?? 'mcr.microsoft.com/azuredocs/containerapps-helloworld:latest'
        name: 'main'
        resources: {
          cpu: json('0.5')
          memory: '1.0Gi'
        }
        args: [
          '--http'
        ]
        env: [
          {
            name: 'OPENAI_API_KEY'
            secretRef: 'openai-api-key'
          }
          {
            name: 'AZURE_STORAGE_CONNECTION_STRING'
            secretRef: 'storage-connection'
          }
        ]
        volumeMounts: [
          {
            volumeName: 'files-volume'
            mountPath: '/files'
          }
        ]
      }
    ]
    volumes: [
      {
        name: 'files-volume'
        storageType: 'AzureFile'
        storageName: filesStorage.name
      }
    ]
    managedIdentities: {
      userAssignedResourceIds: [
        pptTranslatorIdentity.outputs.resourceId
      ]
    }
    registries: [
      {
        server: containerRegistry.outputs.loginServer
        identity: pptTranslatorIdentity.outputs.resourceId
      }
    ]
  }
}

output AZURE_CONTAINER_REGISTRY_ENDPOINT string = containerRegistry.outputs.loginServer
output AZURE_CONTAINER_REGISTRY_NAME string = containerRegistry.outputs.name
output AZURE_RESOURCE_PPT_TRANSLATOR_ID string = pptTranslator.outputs.resourceId
output AZURE_RESOURCE_PPT_TRANSLATOR_NAME string = pptTranslator.outputs.name
output AZURE_RESOURCE_PPT_TRANSLATOR_FQDN string = pptTranslator.outputs.fqdn
output AZURE_STORAGE_ACCOUNT_NAME string = storageAccount.name
output AZURE_STORAGE_FILE_SHARE_NAME string = storageAccount::fileServices::filesShare.name

// azd expects these specific output names for container apps
output SERVICE_PPT_TRANSLATOR_NAME string = pptTranslator.outputs.name
output SERVICE_PPT_TRANSLATOR_IDENTITY_PRINCIPAL_ID string = pptTranslatorIdentity.outputs.principalId
output SERVICE_PPT_TRANSLATOR_IMAGE_NAME string = pptTranslatorFetchLatestImage.outputs.?containers[?0].?image ?? 'mcr.microsoft.com/azuredocs/containerapps-helloworld:latest'

