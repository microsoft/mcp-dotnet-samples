targetScope = 'subscription'

@minLength(1)
@maxLength(64)
@description('Name of the environment that can be used as part of naming resource convention')
param environmentName string

@minLength(1)
@description('Primary location for all resources & Flex Consumption Function App')
@allowed([
  'australiaeast'
  'brazilsouth'
  'eastus'
  'southeastasia'
  'westeurope'
  'southafricanorth'
  'uaenorth'
])
@metadata({
  azd: {
    type: 'location'
  }
})
param location string

param mcpOutlookEmailExists bool

param vnetEnabled bool = true // Enable VNet by default

@description('Id of the user or app to assign application roles')
param principalId string

// Tags that should be applied to all resources.
// 
// Note that 'azd-service-name' tags should be applied separately to service host resources.
// Example usage:
//   tags: union(tags, { 'azd-service-name': <service name in azure.yaml> })
var tags = {
  'azd-env-name': environmentName
}

// Organize resources in a resource group
resource rg 'Microsoft.Resources/resourceGroups@2021-04-01' = {
  name: 'rg-${environmentName}'
  location: location
  tags: tags
}

module resources 'resources.bicep' = {
  scope: rg
  name: 'resources'
  params: {
    location: location
    tags: tags
    principalId: principalId
    mcpOutlookEmailExists: mcpOutlookEmailExists
    azdServiceName: 'outlook-email'
    vnetEnabled: vnetEnabled
  }
}

// output AZURE_CONTAINER_REGISTRY_ENDPOINT string = resources.outputs.AZURE_CONTAINER_REGISTRY_ENDPOINT
output AZURE_RESOURCE_MCP_OUTLOOK_EMAIL_ID string = resources.outputs.AZURE_RESOURCE_MCP_OUTLOOK_EMAIL_ID
output AZURE_RESOURCE_MCP_OUTLOOK_EMAIL_NAME string = resources.outputs.AZURE_RESOURCE_MCP_OUTLOOK_EMAIL_NAME
output AZURE_RESOURCE_MCP_OUTLOOK_EMAIL_FQDN string = resources.outputs.AZURE_RESOURCE_MCP_OUTLOOK_EMAIL_FQDN
output AZURE_RESOURCE_MCP_OUTLOOK_EMAIL_GATEWAY_ID string = resources.outputs.AZURE_RESOURCE_MCP_OUTLOOK_EMAIL_GATEWAY_ID
output AZURE_RESOURCE_MCP_OUTLOOK_EMAIL_GATEWAY_NAME string = resources.outputs.AZURE_RESOURCE_MCP_OUTLOOK_EMAIL_GATEWAY_NAME
output AZURE_RESOURCE_MCP_OUTLOOK_EMAIL_GATEWAY_FQDN string = resources.outputs.AZURE_RESOURCE_MCP_OUTLOOK_EMAIL_GATEWAY_FQDN
