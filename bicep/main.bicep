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
