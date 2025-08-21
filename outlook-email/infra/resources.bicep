@description('The location used for all deployed resources')
param location string = resourceGroup().location

@description('Tags that will be applied to all resources')
param tags object = {}

param mcpOutlookEmailExists bool

param vnetEnabled bool = true // Enable VNet by default

@description('Id of the user or app to assign application roles')
param principalId string

param azdServiceName string

var abbrs = loadJsonContent('./abbreviations.json')
var resourceToken = uniqueString(subscription().id, resourceGroup().id, location)
var functionAppName = '${abbrs.webSitesFunctions}${resourceToken}'
var deploymentStorageContainerName = 'app-package-${take(functionAppName, 32)}-${take(toLower(uniqueString(functionAppName, resourceToken)), 7)}'

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

// User assigned identity
module mcpOutlookEmailIdentity 'br/public:avm/res/managed-identity/user-assigned-identity:0.2.1' = {
  name: 'mcpOutlookEmailIdentity'
  params: {
    name: '${abbrs.managedIdentityUserAssignedIdentities}mcpoutlookemail-${resourceToken}'
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

// MCP Entra App
module mcpEntraApp './modules/mcp-entra-app.bicep' = {
  name: 'mcpEntraApp'
  params: {
    mcpAppUniqueName: 'mcp-outlookemail-${resourceToken}'
    mcpAppDisplayName: 'MCP-OutlookEmail-${resourceToken}'
    userAssignedIdentityPrincipleId: mcpOutlookEmailIdentity.outputs.principalId
    functionAppName: functionAppName
  }
}

// MCP server API endpoints
module mcpApiModule './modules/mcp-api.bicep' = {
  name: 'mcpApiModule'
  params: {
    apimServiceName: apimService.outputs.name
    functionAppName: functionAppName
    mcpAppId: mcpEntraApp.outputs.mcpAppId
    mcpAppTenantId: mcpEntraApp.outputs.mcpAppTenantId
  }
  dependsOn: [
    appServicePlan
    fncapp
    storagePrivateEndpoint
  ]
}

// Create an App Service Plan to group applications under the same payment plan and SKU
module appServicePlan 'br/public:avm/res/web/serverfarm:0.1.1' = {
  name: 'appServicePlan'
  params: {
    name: '${abbrs.webServerFarms}${resourceToken}'
    sku: {
      name: 'FC1'
      tier: 'FlexConsumption'
    }
    reserved: true
    location: location
    tags: tags
  }
}

// Function app
module fncapp './modules/functionapp.bicep' = {
  name: 'functionapp'
  params: {
    name: functionAppName
    location: location
    tags: tags
    azdServiceName: azdServiceName
    applicationInsightsName: monitoring.outputs.applicationInsightsName
    appServicePlanId: appServicePlan.outputs.resourceId
    runtimeName: 'dotnet-isolated'
    runtimeVersion: '9.0'
    storageAccountName: storage.outputs.name
    enableBlob: storageEndpointConfig.enableBlob
    enableQueue: storageEndpointConfig.enableQueue
    enableTable: storageEndpointConfig.enableTable
    deploymentStorageContainerName: deploymentStorageContainerName
    identityId: mcpOutlookEmailIdentity.outputs.resourceId
    identityClientId: mcpOutlookEmailIdentity.outputs.clientId
    appSettings: {}
    virtualNetworkSubnetId: vnetEnabled ? serviceVirtualNetwork!.outputs.appSubnetID : ''
  }
}

// Backing storage for Azure Functions app
module storage 'br/public:avm/res/storage/storage-account:0.8.3' = {
  name: 'storage'
  params: {
    name: '${abbrs.storageStorageAccounts}${resourceToken}'
    allowBlobPublicAccess: false
    allowSharedKeyAccess: false // Disable local authentication methods as per policy
    dnsEndpointType: 'Standard'
    publicNetworkAccess: vnetEnabled ? 'Disabled' : 'Enabled'
    networkAcls: vnetEnabled ? {
      defaultAction: 'Deny'
      bypass: 'None'
    } : {
      defaultAction: 'Allow'
      bypass: 'AzureServices'
    }
    blobServices: {
      containers: [{name: deploymentStorageContainerName}]
    }
    minimumTlsVersion: 'TLS1_2'  // Enforcing TLS 1.2 for better security
    location: location
    tags: tags
  }
}

// Define the configuration object locally to pass to the modules
var storageEndpointConfig = {
  enableBlob: true  // Required for AzureWebJobsStorage, .zip deployment, Event Hubs trigger and Timer trigger checkpointing
  enableQueue: false  // Required for Durable Functions and MCP trigger
  enableTable: false  // Required for Durable Functions and OpenAI triggers and bindings
  enableFiles: false   // Not required, used in legacy scenarios
  allowUserIdentityPrincipal: true   // Allow interactive user identity to access for testing and debugging
}

// Virtual Network & private endpoint to blob storage
module serviceVirtualNetwork './modules/vnet.bicep' =  if (vnetEnabled) {
  name: 'serviceVirtualNetwork'
  params: {
    location: location
    tags: tags
    vNetName: '${abbrs.networkVirtualNetworks}${resourceToken}'
  }
}

// Consolidated Role Assignments
module rbac './modules/rbac.bicep' = {
  name: 'rbacAssignments'
  params: {
    storageAccountName: storage.outputs.name
    appInsightsName: monitoring.outputs.applicationInsightsName
    managedIdentityPrincipalId: mcpOutlookEmailIdentity.outputs.principalId
    userIdentityPrincipalId: principalId
    enableBlob: storageEndpointConfig.enableBlob
    enableQueue: storageEndpointConfig.enableQueue
    enableTable: storageEndpointConfig.enableTable
    allowUserIdentityPrincipal: storageEndpointConfig.allowUserIdentityPrincipal
  }
}

module storagePrivateEndpoint './modules/storage-privateendpoint.bicep' = if (vnetEnabled) {
  name: 'servicePrivateEndpoint'
  params: {
    location: location
    tags: tags
    virtualNetworkName: '${abbrs.networkVirtualNetworks}${resourceToken}'
    subnetName: vnetEnabled ? serviceVirtualNetwork!.outputs.peSubnetName : '' // Keep conditional check for safety, though module won't run if !vnetEnabled
    resourceName: storage.outputs.name
    enableBlob: storageEndpointConfig.enableBlob
    enableQueue: storageEndpointConfig.enableQueue
    enableTable: storageEndpointConfig.enableTable
  }
}

// // Container registry
// module containerRegistry 'br/public:avm/res/container-registry/registry:0.1.1' = {
//   name: 'registry'
//   params: {
//     name: '${abbrs.containerRegistryRegistries}${resourceToken}'
//     location: location
//     tags: tags
//     publicNetworkAccess: 'Enabled'
//     roleAssignments: [
//       {
//         principalId: mcpOutlookEmailIdentity.outputs.principalId
//         principalType: 'ServicePrincipal'
//         // ACR pull role
//         roleDefinitionIdOrName: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '7f951dda-4ed3-4680-a7ca-43fe172d538d')
//       }
//     ]
//   }
// }

// // Container apps environment
// module containerAppsEnvironment 'br/public:avm/res/app/managed-environment:0.4.5' = {
//   name: 'container-apps-environment'
//   params: {
//     logAnalyticsWorkspaceResourceId: monitoring.outputs.logAnalyticsWorkspaceResourceId
//     name: '${abbrs.appManagedEnvironments}${resourceToken}'
//     location: location
//     zoneRedundant: false
//   }
// }

// // Azure Container Apps
// module mcpOutlookEmailFetchLatestImage './modules/fetch-container-image.bicep' = {
//   name: 'mcpOutlookEmail-fetch-image'
//   params: {
//     exists: mcpOutlookEmailExists
//     name: 'outlook-email'
//   }
// }

// module mcpOutlookEmail 'br/public:avm/res/app/container-app:0.8.0' = {
//   name: 'mcpOutlookEmail'
//   params: {
//     name: 'outlook-email'
//     ingressTargetPort: 8080
//     scaleMinReplicas: 1
//     scaleMaxReplicas: 10
//     secrets: {
//       secureList: [
//       ]
//     }
//     containers: [
//       {
//         image: mcpOutlookEmailFetchLatestImage.outputs.?containers[?0].?image ?? 'mcr.microsoft.com/azuredocs/containerapps-helloworld:latest'
//         name: 'main'
//         resources: {
//           cpu: json('0.5')
//           memory: '1.0Gi'
//         }
//         env: [
//           {
//             name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
//             value: monitoring.outputs.applicationInsightsConnectionString
//           }
//           {
//             name: 'AZURE_CLIENT_ID'
//             value: mcpOutlookEmailIdentity.outputs.clientId
//           }
//           {
//             name: 'PORT'
//             value: '8080'
//           }
//         ]
//         args: [
//           '--http'
//         ]
//       }
//     ]
//     managedIdentities: {
//       systemAssigned: false
//       userAssignedResourceIds: [
//         mcpOutlookEmailIdentity.outputs.resourceId
//       ]
//     }
//     registries: [
//       {
//         server: containerRegistry.outputs.loginServer
//         identity: mcpOutlookEmailIdentity.outputs.resourceId
//       }
//     ]
//     environmentResourceId: containerAppsEnvironment.outputs.resourceId
//     corsPolicy: {
//       allowedOrigins: [
//         'https://make.preview.powerapps.com'
//         'https://make.powerapps.com'
//         'https://make.preview.powerautomate.com'
//         'https://make.powerautomate.com'
//         'https://copilotstudio.preview.microsoft.com'
//         'https://copilotstudio.microsoft.com'
//       ]
//     }
//     location: location
//     tags: union(tags, { 'azd-service-name': 'outlook-email' })
//   }
// }

// output AZURE_CONTAINER_REGISTRY_ENDPOINT string = containerRegistry.outputs.loginServer
// output AZURE_RESOURCE_MCP_OUTLOOK_EMAIL_ID string = mcpOutlookEmail.outputs.resourceId
// output AZURE_RESOURCE_MCP_OUTLOOK_EMAIL_NAME string = mcpOutlookEmail.outputs.name
// output AZURE_RESOURCE_MCP_OUTLOOK_EMAIL_FQDN string = mcpOutlookEmail.outputs.fqdn

output AZURE_RESOURCE_MCP_OUTLOOK_EMAIL_ID string = fncapp.outputs.resourceId
output AZURE_RESOURCE_MCP_OUTLOOK_EMAIL_NAME string = fncapp.outputs.name
output AZURE_RESOURCE_MCP_OUTLOOK_EMAIL_FQDN string = fncapp.outputs.fqdn

output AZURE_RESOURCE_MCP_OUTLOOK_EMAIL_GATEWAY_ID string = apimService.outputs.id
output AZURE_RESOURCE_MCP_OUTLOOK_EMAIL_GATEWAY_NAME string = apimService.outputs.name
output AZURE_RESOURCE_MCP_OUTLOOK_EMAIL_GATEWAY_FQDN string = replace(apimService.outputs.gatewayUrl, 'https://', '')
