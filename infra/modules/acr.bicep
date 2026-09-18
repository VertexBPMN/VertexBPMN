@description('Azure region for the container registry.')
param location string

@description('Name of the container registry.')
param name string

@description('Environment name used for tagging (e.g. dev, staging, prod).')
param environment string

resource registry 'Microsoft.ContainerRegistry/registries@2024-11-01-preview' = {
  name: name
  location: location
  sku: {
    name: 'Premium'
  }
  tags: {
    Environment: environment
  }
  properties: {
    adminUserEnabled: false
    dataEndpointEnabled: false
    networkRuleBypassOptions: 'AzureServices'
    publicNetworkAccess: 'Disabled'
    anonymousPullEnabled: false
  }
}

output registryId string = registry.id
output registryLoginServer string = registry.properties.loginServer
