targetScope = 'subscription'

@description('Environment name used for naming resources')
@minLength(1)
@maxLength(64)
param environmentName string

@description('Primary deployment region')
@minLength(1)
param location string

param pptTranslatorExists bool

@description('Id of the user or app to assign application roles')
param principalId string

@secure()
@description('OpenAI API Key')
param openAiApiKey string

var tags = {
  'azd-env-name': environmentName
}

resource rg 'Microsoft.Resources/resourceGroups@2021-04-01' = {
  name: 'rg-${environmentName}'
  location: location
  tags: tags
}

module resources './resources.bicep' = {
  name: 'ppt-translator-resources'
  scope: rg
  params: {
    location: location
    tags: tags
    openAiApiKey: openAiApiKey
    principalId: principalId
    pptTranslatorExists: pptTranslatorExists
  }
}

// Outputs following azd naming conventions
output AZURE_CONTAINER_REGISTRY_ENDPOINT string = resources.outputs.AZURE_CONTAINER_REGISTRY_ENDPOINT
output AZURE_CONTAINER_REGISTRY_NAME string = resources.outputs.AZURE_CONTAINER_REGISTRY_NAME
output AZURE_RESOURCE_PPT_TRANSLATOR_ID string = resources.outputs.AZURE_RESOURCE_PPT_TRANSLATOR_ID
output AZURE_RESOURCE_PPT_TRANSLATOR_NAME string = resources.outputs.AZURE_RESOURCE_PPT_TRANSLATOR_NAME
output AZURE_RESOURCE_PPT_TRANSLATOR_FQDN string = resources.outputs.AZURE_RESOURCE_PPT_TRANSLATOR_FQDN
output AZURE_STORAGE_ACCOUNT_NAME string = resources.outputs.AZURE_STORAGE_ACCOUNT_NAME
output AZURE_STORAGE_FILE_SHARE_NAME string = resources.outputs.AZURE_STORAGE_FILE_SHARE_NAME

// Service-specific outputs for azd
output SERVICE_PPT_TRANSLATOR_NAME string = resources.outputs.SERVICE_PPT_TRANSLATOR_NAME
output SERVICE_PPT_TRANSLATOR_IDENTITY_PRINCIPAL_ID string = resources.outputs.SERVICE_PPT_TRANSLATOR_IDENTITY_PRINCIPAL_ID
output SERVICE_PPT_TRANSLATOR_IMAGE_NAME string = resources.outputs.SERVICE_PPT_TRANSLATOR_IMAGE_NAME
