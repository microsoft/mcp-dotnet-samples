@description('Azure region where resources will be deployed')
param location string = resourceGroup().location

@description('Tags applied to all resources')
param tags object = {}

@description('Id of the user or app to assign application roles')
param principalId string

param pptTranslatorExists bool

@secure()
param openAiApiKey string

var abbrs = loadJsonContent('./abbreviations.json')
var resourceToken = uniqueString(subscription().id, resourceGroup().id, location)
var containerAppName = 'ppt-translator'

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
        shareQuota: 5120  // 5TB
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
  dependsOn: [
    containerAppsEnvironment
  ]
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
module pptTranslatorFetchLatestImage './modules/fetch-container-image.bicep' = {
  name: 'pptTranslator-fetch-image'
  params: {
    exists: pptTranslatorExists
    name: containerAppName
  }
}

resource pptTranslator 'Microsoft.App/containerApps@2023-05-01' = {
  name: containerAppName
  location: location
  tags: union(tags, { 'azd-service-name': 'ppt-translator' })
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${resourceGroup().id}/providers/Microsoft.ManagedIdentity/userAssignedIdentities/${abbrs.managedIdentityUserAssignedIdentities}ppttrans-${resourceToken}': {}
    }
  }
  properties: {
    managedEnvironmentId: containerAppsEnvironment.outputs.resourceId
    configuration: {
      activeRevisionsMode: 'Single'
      ingress: {
        external: true
        targetPort: 8080
        transport: 'auto'
      }
      registries: [
        {
          server: containerRegistry.outputs.loginServer
          identity: pptTranslatorIdentity.outputs.resourceId
        }
      ]
      secrets: [
        {
          name: 'openai-api-key'
          value: !empty(openAiApiKey) ? openAiApiKey : 'placeholder'
        }
        {
          name: 'storage-connection'
          value: 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};AccountKey=${storageAccount.listKeys().keys[0].value};EndpointSuffix=${environment().suffixes.storage}'
        }
      ]
    }
    template: {
      scale: {
        minReplicas: 1
        maxReplicas: 10
      }
      volumes: [
        {
          name: 'files-volume'
          storageType: 'AzureFile'
          storageName: filesStorage.name
        }
      ]
      containers: [
        {
          name: 'main'
          image: pptTranslatorFetchLatestImage.outputs.?containers[?0].?image ?? 'mcr.microsoft.com/azuredocs/containerapps-helloworld:latest'
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
    }
  }
}

output AZURE_CONTAINER_REGISTRY_ENDPOINT string = containerRegistry.outputs.loginServer
output AZURE_CONTAINER_REGISTRY_NAME string = containerRegistry.outputs.name
output AZURE_RESOURCE_PPT_TRANSLATOR_ID string = pptTranslator.id
output AZURE_RESOURCE_PPT_TRANSLATOR_NAME string = pptTranslator.name
output AZURE_RESOURCE_PPT_TRANSLATOR_FQDN string = pptTranslator.properties.configuration.ingress.fqdn
output AZURE_STORAGE_ACCOUNT_NAME string = storageAccount.name
output AZURE_STORAGE_FILE_SHARE_NAME string = storageAccount::fileServices::filesShare.name

