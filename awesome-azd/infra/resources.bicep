@description('The location used for all deployed resources')
param location string = resourceGroup().location

@description('Tags that will be applied to all resources')
param tags object = {}

param mcpAwesomeAzdExists bool

@description('Id of the user or app to assign application roles')
param principalId string

var abbrs = loadJsonContent('./abbreviations.json')
var resourceToken = uniqueString(subscription().id, resourceGroup().id, location)

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
        principalId: mcpAwesomeAzdIdentity.outputs.principalId
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

// User assigned identity
module mcpAwesomeAzdIdentity 'br/public:avm/res/managed-identity/user-assigned-identity:0.2.1' = {
  name: 'mcpAwesomeAzdIdentity'
  params: {
    name: '${abbrs.managedIdentityUserAssignedIdentities}mcpawesomeazd-${resourceToken}'
    location: location
  }
}

// Azure Container Apps
module mcpAwesomeAzdFetchLatestImage './modules/fetch-container-image.bicep' = {
  name: 'mcpAwesomeAzd-fetch-image'
  params: {
    exists: mcpAwesomeAzdExists
    name: 'awesome-azd'
  }
}

module mcpAwesomeAzd 'br/public:avm/res/app/container-app:0.8.0' = {
  name: 'mcpAwesomeAzd'
  params: {
    name: 'awesome-azd'
    ingressTargetPort: 8080
    scaleMinReplicas: 1
    scaleMaxReplicas: 10
    secrets: {
      secureList: [
      ]
    }
    containers: [
      {
        image: mcpAwesomeAzdFetchLatestImage.outputs.?containers[?0].?image ?? 'mcr.microsoft.com/azuredocs/containerapps-helloworld:latest'
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
            value: mcpAwesomeAzdIdentity.outputs.clientId
          }
          {
            name: 'PORT'
            value: '8080'
          }
        ]
        args: [
          '--http'
        ]
      }
    ]
    managedIdentities: {
      systemAssigned: false
      userAssignedResourceIds: [
        mcpAwesomeAzdIdentity.outputs.resourceId
      ]
    }
    registries: [
      {
        server: containerRegistry.outputs.loginServer
        identity: mcpAwesomeAzdIdentity.outputs.resourceId
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
    tags: union(tags, { 'azd-service-name': 'awesome-azd' })
  }
}

output AZURE_CONTAINER_REGISTRY_ENDPOINT string = containerRegistry.outputs.loginServer
output AZURE_RESOURCE_MCP_AWESOME_AZD_ID string = mcpAwesomeAzd.outputs.resourceId
output AZURE_RESOURCE_MCP_AWESOME_AZD_NAME string = mcpAwesomeAzd.outputs.name
output AZURE_RESOURCE_MCP_AWESOME_AZD_FQDN string = mcpAwesomeAzd.outputs.fqdn
