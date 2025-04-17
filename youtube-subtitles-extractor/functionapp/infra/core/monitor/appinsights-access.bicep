param principalID string
param roleDefinitionID string
param appInsightsName string

resource applicationInsights 'Microsoft.Insights/components@2020-02-02' existing = {
  name: appInsightsName
}

// Allow access from API to app insights using a managed identity and least priv role
resource appInsightsRoleAssignment 'Microsoft.Authorization/roleAssignments@2020-04-01-preview' = {
  name: guid(applicationInsights.id, principalID, roleDefinitionID)
  scope: applicationInsights
  properties: {
    roleDefinitionId: resourceId('Microsoft.Authorization/roleDefinitions', roleDefinitionID)
    principalId: principalID
    principalType: 'ServicePrincipal' // Workaround for https://learn.microsoft.com/en-us/azure/role-based-access-control/role-assignments-template#new-service-principal
  }
}

output ROLE_ASSIGNMENT_NAME string = appInsightsRoleAssignment.name

