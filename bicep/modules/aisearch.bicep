@description('Azure location')
param location string = resourceGroup().location

@description('AI Search service name')
param name string

@description('Tags applied to the search service')
param tags object = {}

@description('SKU for the search service')
param sku string = 'basic'

@description('Number of search replicas')
param replicaCount int = 1

@description('Number of search partitions')
param partitionCount int = 1

resource searchService 'Microsoft.Search/searchServices@2024-06-01-preview' = {
  name: name
  location: location
  tags: tags
  identity: {
    type: 'SystemAssigned'
  }
  sku: {
    name: sku
  }
  properties: {
    replicaCount: replicaCount
    partitionCount: partitionCount
    hostingMode: 'default'
    publicNetworkAccess: 'enabled'
    semanticSearch: 'standard'
    authOptions: {
      aadOrApiKey: {
        aadAuthFailureMode: 'http401WithBearerChallenge'
      }
    }
  }
}

output id string = searchService.id
output name string = searchService.name
output endpoint string = 'https://${searchService.name}.search.windows.net'
output principalId string = searchService.identity.principalId
