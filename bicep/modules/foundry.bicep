@description('Azure location')
param location string

@description('AI Services account name (serves as Foundry hub)')
param foundryServicesName string

@description('AI Foundry project name')
param foundryProjectName string

@description('Optional. AI Search endpoint. When set, an Azure AI Search connection is created on the account and project.')
param aiSearchEndpoint string = ''

@description('Optional. AI Search resource id. Required when aiSearchEndpoint is provided.')
param aiSearchResourceId string = ''


// Azure AI Services account with project management enabled
resource foundrySvc 'Microsoft.CognitiveServices/accounts@2025-10-01-preview' = {
  name: foundryServicesName
  location: location
  identity: {
    type: 'SystemAssigned'
  }
  sku: {
    name: 'S0'
  }
  kind: 'AIServices'
  properties: {
    allowProjectManagement: true
    customSubDomainName: foundryServicesName
    publicNetworkAccess: 'Enabled'
    disableLocalAuth: true
    networkAcls: {
      defaultAction: 'Allow'
    }
  }
}

// Azure AI Foundry Project
resource aiProject 'Microsoft.CognitiveServices/accounts/projects@2025-06-01' = {
  parent: foundrySvc
  name: foundryProjectName
  location: location
  identity: {
    type: 'SystemAssigned'
  }
  properties: {}
}

// Azure AI Search connection on the account
resource aiSearchConnection 'Microsoft.CognitiveServices/accounts/connections@2025-10-01-preview' = if (aiSearchEndpoint != '') {
  parent: foundrySvc
  name: 'ai-search-connection'
  properties: {
    authType: 'AAD'
    category: 'CognitiveSearch'
    target: aiSearchEndpoint
    isSharedToAll: true
    metadata: {
      type: 'azure_ai_search'
      ResourceId: aiSearchResourceId
      useWorkspaceManagedIdentity: 'false'
    }
  }
}

// Azure AI Search connection on the project
resource aiSearchProjectConnection 'Microsoft.CognitiveServices/accounts/projects/connections@2025-10-01-preview' = if (aiSearchEndpoint != '') {
  parent: aiProject
  name: 'ai-search-connection-project'
  properties: {
    authType: 'AAD'
    category: 'CognitiveSearch'
    target: aiSearchEndpoint
    metadata: {
      type: 'azure_ai_search'
      ResourceId: aiSearchResourceId
      useWorkspaceManagedIdentity: 'false'
    }
  }
}

// gpt-5.4 model deployment
resource gpt54Deployment 'Microsoft.CognitiveServices/accounts/deployments@2024-10-01' = {
  parent: foundrySvc
  name: 'gpt-5.4'
  sku: {
    name: 'GlobalStandard'
    capacity: 900
  }
  properties: {
    model: {
      format: 'OpenAI'
      name: 'gpt-5.4'
      version: '2026-03-05'
    }
    versionUpgradeOption: 'OnceNewDefaultVersionAvailable'
    raiPolicyName: 'Microsoft.DefaultV2'
  }
}

output aiProjectEndpoint string = aiProject.properties.endpoints['AI Foundry API']
output aiServicesEndpoint string = foundrySvc.properties.endpoint
output modelDeploymentName string = gpt54Deployment.name
output aiHubPrincipalId string = foundrySvc.identity.principalId
output aiProjectPrincipalId string = aiProject.identity.principalId
output aiSearchConnectionId string = aiSearchEndpoint != '' ? aiSearchConnection.id : ''
output aiSearchProjectConnectionId string = aiSearchEndpoint != '' ? aiSearchProjectConnection.id : ''
