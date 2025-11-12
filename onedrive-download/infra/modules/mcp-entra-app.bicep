extension microsoftGraphV1

@description('The name of the MCP Entra application')
param mcpAppUniqueName string

@description('The display name of the MCP Entra application')
param mcpAppDisplayName string

@description('Tenant ID where the application is registered')
param tenantId string = tenant().tenantId

@description('Provide an array of Microsoft Graph scopes like "User.Read"')
param appScopes array = ['User.Read']

@description('Provide an array of Microsoft Graph roles like "Mail.Send"')
param appRoles array = ['Mail.Send']

// Microsoft Graph app ID
var graphAppId = '00000003-0000-0000-c000-000000000000'
var msGraphAppId = graphAppId

// VS Code app ID
var vscodeAppId = 'aebc6443-996d-45c2-90f0-388ff96faa56'

// Permission ID
var applicationMailSendPermissionId = 'b633e1c5-b582-4048-a93e-9f11b44c7e96'

// Get the Microsoft Graph service principal so that the scope names
// can be looked up and mapped to a permission ID
resource msGraphSP 'Microsoft.Graph/servicePrincipals@v1.0' existing = {
  appId: graphAppId
}

var graphScopes = msGraphSP.oauth2PermissionScopes
var graphRoles = msGraphSP.appRoles

var scopes = map(filter(graphScopes, scope => contains(appScopes, scope.value)), scope => {
  id: scope.id
  type: 'Scope'
})
var roles = map(filter(graphRoles, role => contains(appRoles, role.value)), role => {
  id: role.id
  type: 'Role'
})

var permissionId = guid(mcpAppUniqueName, 'user_impersonation')
resource mcpEntraApp 'Microsoft.Graph/applications@v1.0' = {
  displayName: mcpAppDisplayName
  uniqueName: mcpAppUniqueName
  api: {
    oauth2PermissionScopes: [
      {
        id: permissionId
        adminConsentDescription: 'Allows the application to access MCP resources on behalf of the signed-in user'
        adminConsentDisplayName: 'Access MCP resources'
        isEnabled: true
        type: 'User'
        userConsentDescription: 'Allows the app to access MCP resources on your behalf'
        userConsentDisplayName: 'Access MCP resources'
        value: 'user_impersonation'
      }
    ]
    requestedAccessTokenVersion: 2
    preAuthorizedApplications: [
      {
        appId: vscodeAppId
        delegatedPermissionIds: [
          guid(mcpAppUniqueName, 'user_impersonation')
        ]
      }
    ]
  }
  // Parameterized Microsoft Graph delegated scopes based on appScopes
  requiredResourceAccess: [
    {
      resourceAppId: msGraphAppId // Microsoft Graph
      resourceAccess: concat(scopes, roles)
    }
  ]
}

resource applicationRegistrationServicePrincipal 'Microsoft.Graph/servicePrincipals@v1.0' = {
  appId: mcpEntraApp.appId
}

resource applicationPermissionGrantForApp 'Microsoft.Graph/appRoleAssignedTo@v1.0' = {
  resourceId: msGraphSP.id
  appRoleId: applicationMailSendPermissionId
  principalId: applicationRegistrationServicePrincipal.id
}

// Deployment script to generate client secret
resource generateClientSecret 'Microsoft.Resources/deploymentScripts@2023-08-01' = {
  name: '${mcpAppUniqueName}-secret-generator'
  location: resourceGroup().location
  kind: 'AzureCLI'
  properties: {
    azCliVersion: '2.53.0' // Specify a recent Azure CLI version
    scriptContent: '''
      #!/bin/bash
      echo "Generating client secret for app: ${mcpEntraApp.id}"
      secretValue=$(az ad app credential reset --id "${mcpEntraApp.id}" --query "password" -o tsv)
      outputJson=$(printf '{"clientSecret": "%s"}' "$secretValue")
      echo $outputJson > $AZ_SCRIPTS_OUTPUT_PATH
    '''
    retentionInterval: 'P1D' // Keep script for 1 day
    forceUpdateTag: 'v1.0' // Force script to run on every deployment
    timeout: 'PT10M' // 10 minutes timeout
  }
}

// Outputs
output mcpAppId string = mcpEntraApp.appId
output mcpAppTenantId string = tenantId
output mcpAppClientSecret string = generateClientSecret.properties.outputs.clientSecret
