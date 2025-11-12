extension microsoftGraphV1

@description('The name of the MCP Entra application')
param mcpAppUniqueName string

@description('The display name of the MCP Entra application')
param mcpAppDisplayName string

@description('Tenant ID where the application is registered')
param tenantId string = tenant().tenantId

@description('The principle id of the user-assigned managed identity')
param userAssignedIdentityPrincipleId string

@description('The web app name for callback URL configuration')
param functionAppName string

@description('Provide an array of Microsoft Graph scopes like "User.Read"')
param appScopes array = ['User.Read']

@description('Provide an array of Microsoft Graph roles like "Mail.Send"')
param appRoles array = ['Mail.Send']

var loginEndpoint = environment().authentication.loginEndpoint
var issuer = '${loginEndpoint}${tenantId}/v2.0'

// Microsoft Graph app ID
var graphAppId = '00000003-0000-0000-c000-000000000000'
var msGraphAppId = graphAppId

// VS Code app ID
var vscodeAppId = 'aebc6443-996d-45c2-90f0-388ff96faa56'

// Permission ID
var delegatedUserReadPermissionId = 'e1fe6dd8-ba31-4d61-89e7-88639da4683d'
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
  spa: {
    redirectUris: [
      'https://${functionAppName}.azurewebsites.net/auth/callback'
    ]
  }

  resource fic 'federatedIdentityCredentials@v1.0' = {
    name: '${mcpEntraApp.uniqueName}/msiAsFic'
    description: 'Trust the user-assigned MI as a credential for the MCP app'
    audiences: [
       'api://AzureADTokenExchange'
    ]
    issuer: issuer
    subject: userAssignedIdentityPrincipleId
  }
}

resource applicationRegistrationServicePrincipal 'Microsoft.Graph/servicePrincipals@v1.0' = {
  appId: mcpEntraApp.appId
}

resource applicationPermissionGrantForApp 'Microsoft.Graph/appRoleAssignedTo@v1.0' = {
  resourceId: msGraphSP.id
  appRoleId: applicationMailSendPermissionId
  principalId: applicationRegistrationServicePrincipal.id
}

resource applicationPermissionGrantForUserAssignedIdentity 'Microsoft.Graph/appRoleAssignedTo@v1.0' = {
  resourceId: msGraphSP.id
  appRoleId: applicationMailSendPermissionId
  principalId: userAssignedIdentityPrincipleId
}

// Outputs
output mcpAppId string = mcpEntraApp.appId
output mcpAppTenantId string = tenantId
