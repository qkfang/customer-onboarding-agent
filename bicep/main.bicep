targetScope = 'resourceGroup'

@description('Azure location')
param location string = resourceGroup().location

@description('Project abbreviation for resources')
@minLength(1)
param projectAbbr string

@description('Project abbreviation for resources')
@minLength(1)
param projectName string

@description('SKU for App Service plan')
@allowed([
  'F1'
  'B1'
  'S1'
])
param appServiceSku string = 'S1'

@description('Blob container name for noise log files')
param logsContainerName string = 'noise-logs'

@description('Additional principals to grant Storage Blob Data Contributor on the storage account')
param principals array = []

@description('UPN/email addresses of Fabric capacity administrators')
param fabricAdminMembers array = []

var logAnalyticsName = '${projectAbbr}-law'
var appInsightsName = '${projectAbbr}-appi'
var storageAccountName = toLower('${projectAbbr}sa')

var appServicePlanName = '${projectAbbr}-plan'
var webAppName = '${projectAbbr}-web'

var aiProjectName = '${projectAbbr}-proj'
var aiServicesName = '${projectAbbr}-ais'
var aiSearchName = '${projectAbbr}-search'
var bingSearchName = '${projectAbbr}-bing'
var fabricCapacityName = '${projectAbbr}fabric'


module monitoring './modules/monitoring.bicep' = {
  name: 'monitoring'
  params: {
    location: location
    logAnalyticsName: logAnalyticsName
    appInsightsName: appInsightsName
  }
}

module storage './modules/storage.bicep' = {
  name: 'storage'
  params: {
    location: location
    storageAccountName: storageAccountName
    logsContainerName: logsContainerName
  }
}

module foundry './modules/foundry.bicep' = {
  name: 'foundry'
  params: {
    location: location
    foundryServicesName: aiServicesName
    foundryProjectName: aiProjectName
    aiSearchEndpoint: aiSearch.outputs.endpoint
    aiSearchResourceId: aiSearch.outputs.id
  }
}

module aiSearch './modules/aisearch.bicep' = {
  name: 'aisearch'
  params: {
    location: location
    name: aiSearchName
  }
}

module bing './modules/bing.bicep' = {
  name: 'bing'
  params: {
    bingSearchName: bingSearchName
  }
}

module fabric './modules/fabric.bicep' = {
  name: 'fabric'
  params: {
    location: location
    capacityName: fabricCapacityName
    skuName: 'F2'
    adminMembers: fabricAdminMembers
  }
}


module appService './modules/appservice.bicep' = {
  name: 'appservice'
  params: {
    location: location
    webAppName: webAppName
    appServicePlanName: appServicePlanName
    appServiceSku: appServiceSku
    appInsightsConnectionString: monitoring.outputs.appInsightsConnectionString
    storageAccountName: storageAccountName
    logsContainerName: logsContainerName
  }
}


resource storageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' existing = {
  name: storageAccountName
}

resource blobDataContributorAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: storageAccount
  name: guid(storageAccountName, webAppName, 'ba92f5b4-2d11-453d-a403-e96b0029c9fe')
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'ba92f5b4-2d11-453d-a403-e96b0029c9fe')
    principalId: appService.outputs.principalId
    principalType: 'ServicePrincipal'
  }
  dependsOn: [
    storage
  ]
}

resource principalBlobDataContributorAssignments 'Microsoft.Authorization/roleAssignments@2022-04-01' = [for principal in principals: {
  scope: storageAccount
  name: guid(storageAccountName, principal.id, 'ba92f5b4-2d11-453d-a403-e96b0029c9fe')
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'ba92f5b4-2d11-453d-a403-e96b0029c9fe')
    principalId: principal.id
    principalType: principal.principalType
  }
  dependsOn: [
    storage
  ]
}]

resource searchService 'Microsoft.Search/searchServices@2024-06-01-preview' existing = {
  name: aiSearchName
}

// Foundry account managed identity -> Search Index Data Reader on the search service
resource foundrySearchIndexDataReader 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: searchService
  name: guid(aiSearchName, aiServicesName, '1407120a-92aa-4202-b7e9-c0e197c71c8f')
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '1407120a-92aa-4202-b7e9-c0e197c71c8f')
    principalId: foundry.outputs.aiHubPrincipalId
    principalType: 'ServicePrincipal'
  }
  dependsOn: [
    aiSearch
  ]
}

// Foundry account managed identity -> Search Service Contributor on the search service
resource foundrySearchServiceContributor 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: searchService
  name: guid(aiSearchName, aiServicesName, '7ca78c08-252a-4471-8644-bb5ff32d4ba0')
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '7ca78c08-252a-4471-8644-bb5ff32d4ba0')
    principalId: foundry.outputs.aiHubPrincipalId
    principalType: 'ServicePrincipal'
  }
  dependsOn: [
    aiSearch
  ]
}

// Foundry project managed identity -> Search Index Data Reader on the search service
resource foundryProjectSearchIndexDataReader 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: searchService
  name: guid(aiSearchName, aiProjectName, '1407120a-92aa-4202-b7e9-c0e197c71c8f')
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '1407120a-92aa-4202-b7e9-c0e197c71c8f')
    principalId: foundry.outputs.aiProjectPrincipalId
    principalType: 'ServicePrincipal'
  }
  dependsOn: [
    aiSearch
  ]
}
