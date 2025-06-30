@description('The location used for all deployed resources')
param location string = resourceGroup().location

@description('Tags that will be applied to all resources')
param tags object = {}

param mcpAssemblyAnalyzerExists bool

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
        principalId: mcpAssemblyAnalyzerIdentity.outputs.principalId
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
module mcpAssemblyAnalyzerIdentity 'br/public:avm/res/managed-identity/user-assigned-identity:0.2.1' = {
  name: 'mcpAssemblyAnalyzerIdentity'
  params: {
    name: '${abbrs.managedIdentityUserAssignedIdentities}mcpassemblyanalyzer-${resourceToken}'
    location: location
  }
}

// Azure Container Apps
module mcpAssemblyAnalyzerFetchLatestImage './modules/fetch-container-image.bicep' = {
  name: 'mcpAssemblyAnalyzer-fetch-image'
  params: {
    exists: mcpAssemblyAnalyzerExists
    name: 'mcp-assemblyanalyzer'
  }
}

module mcpAssemblyAnalyzer 'br/public:avm/res/app/container-app:0.8.0' = {
  name: 'mcpAssemblyAnalyzer'
  params: {
    name: 'mcp-assemblyanalyzer'
    ingressTargetPort: 8080
    scaleMinReplicas: 1
    scaleMaxReplicas: 10
    secrets: {
      secureList: [
      ]
    }
    containers: [
      {
        image: mcpAssemblyAnalyzerFetchLatestImage.outputs.?containers[?0].?image ?? 'mcr.microsoft.com/azuredocs/containerapps-helloworld:latest'
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
            value: mcpAssemblyAnalyzerIdentity.outputs.clientId
          }
          {
            name: 'PORT'
            value: '8080'
          }
        ]
      }
    ]
    managedIdentities: {
      systemAssigned: false
      userAssignedResourceIds: [
        mcpAssemblyAnalyzerIdentity.outputs.resourceId
      ]
    }
    registries: [
      {
        server: containerRegistry.outputs.loginServer
        identity: mcpAssemblyAnalyzerIdentity.outputs.resourceId
      }
    ]
    environmentResourceId: containerAppsEnvironment.outputs.resourceId
    location: location
    tags: union(tags, { 'azd-service-name': 'mcp-assemblyanalyzer' })
  }
}

output AZURE_CONTAINER_REGISTRY_ENDPOINT string = containerRegistry.outputs.loginServer
output AZURE_RESOURCE_MCP_ASSEMBLYANALYZER_ID string = mcpAssemblyAnalyzer.outputs.resourceId
output AZURE_RESOURCE_MCP_ASSEMBLYANALYZER_NAME string = mcpAssemblyAnalyzer.outputs.name
output AZURE_RESOURCE_MCP_ASSEMBLYANALYZER_FQDN string = mcpAssemblyAnalyzer.outputs.fqdn
