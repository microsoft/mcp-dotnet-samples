@description('The location used for all deployed resources')
param location string = resourceGroup().location

@description('Tags that will be applied to all resources')
param tags object = {}

param mcpMd2HtmlExists bool

@description('Id of the user or app to assign application roles')
param principalId string

param forTechCommunity bool = true
param addExtraParagraph bool = true
param tagsForExtraParagraph string = 'p,blockquote,h1,h2,h3,h4,h5,h6,ol,ul,dl'

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
        principalId: mcpMd2HtmlIdentity.outputs.principalId
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
module mcpMd2HtmlIdentity 'br/public:avm/res/managed-identity/user-assigned-identity:0.2.1' = {
  name: 'mcpMd2HtmlIdentity'
  params: {
    name: '${abbrs.managedIdentityUserAssignedIdentities}mcpmd2html-${resourceToken}'
    location: location
  }
}

// Azure Container Apps
module mcpMd2HtmlFetchLatestImage './modules/fetch-container-image.bicep' = {
  name: 'mcpMd2Html-fetch-image'
  params: {
    exists: mcpMd2HtmlExists
    name: 'mcp-md2html'
  }
}

module mcpMd2Html 'br/public:avm/res/app/container-app:0.8.0' = {
  name: 'mcpMd2Html'
  params: {
    name: 'mcp-md2html'
    ingressTargetPort: 8080
    scaleMinReplicas: 1
    scaleMaxReplicas: 10
    secrets: {
      secureList: [
      ]
    }
    containers: [
      {
        image: mcpMd2HtmlFetchLatestImage.outputs.?containers[?0].?image ?? 'mcr.microsoft.com/azuredocs/containerapps-helloworld:latest'
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
            value: mcpMd2HtmlIdentity.outputs.clientId
          }
          {
            name: 'PORT'
            value: '8080'
          }
          {
            name: 'Html__TechCommunity'
            value: '${forTechCommunity}'
          }
          {
            name: 'Html__ExtraParagraph'
            value: '${addExtraParagraph}'
          }
          {
            name: 'Html__Tags'
            value: tagsForExtraParagraph
          }
        ]
      }
    ]
    managedIdentities: {
      systemAssigned: false
      userAssignedResourceIds: [
        mcpMd2HtmlIdentity.outputs.resourceId
      ]
    }
    registries: [
      {
        server: containerRegistry.outputs.loginServer
        identity: mcpMd2HtmlIdentity.outputs.resourceId
      }
    ]
    environmentResourceId: containerAppsEnvironment.outputs.resourceId
    location: location
    tags: union(tags, { 'azd-service-name': 'mcp-md2html' })
  }
}

output AZURE_CONTAINER_REGISTRY_ENDPOINT string = containerRegistry.outputs.loginServer
output AZURE_RESOURCE_MCP_MD2HTML_ID string = mcpMd2Html.outputs.resourceId
output AZURE_RESOURCE_MCP_MD2HTML_NAME string = mcpMd2Html.outputs.name
output AZURE_RESOURCE_MCP_MD2HTML_FQDN string = mcpMd2Html.outputs.fqdn
