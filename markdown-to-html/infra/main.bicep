targetScope = 'subscription'

@minLength(1)
@maxLength(64)
@description('Name of the environment that can be used as part of naming resource convention')
param environmentName string

@minLength(1)
@description('Primary location for all resources')
param location string

param mcpMd2HtmlExists bool

@description('Id of the user or app to assign application roles')
param principalId string

param forTechCommunity bool = true
param addExtraParagraph bool = true
param tagsForExtraParagraph string = 'p,blockquote,h1,h2,h3,h4,h5,h6,ol,ul,dl'

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
    mcpMd2HtmlExists: mcpMd2HtmlExists
    forTechCommunity: forTechCommunity
    addExtraParagraph: addExtraParagraph
    tagsForExtraParagraph: tagsForExtraParagraph
  }
}

output AZURE_CONTAINER_REGISTRY_ENDPOINT string = resources.outputs.AZURE_CONTAINER_REGISTRY_ENDPOINT
output AZURE_RESOURCE_MCP_MD2HTML_ID string = resources.outputs.AZURE_RESOURCE_MCP_MD2HTML_ID
output AZURE_RESOURCE_MCP_MD2HTML_NAME string = resources.outputs.AZURE_RESOURCE_MCP_MD2HTML_NAME
output AZURE_RESOURCE_MCP_MD2HTML_FQDN string = resources.outputs.AZURE_RESOURCE_MCP_MD2HTML_FQDN
