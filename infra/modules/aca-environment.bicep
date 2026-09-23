@description('Location for all resources.')
param location string

@description('Name of the Managed Environment.')
param name string

@description('Environment tag value.')
param environment string

@description('Resource ID of the subnet delegated to Azure Container Apps.')
param acaSubnetId string

resource managedEnvironment 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: name
  location: location
  tags: {
    Environment: environment
  }
  properties: {
    vnetConfiguration: {
      infrastructureSubnetId: acaSubnetId
    }
    zoneRedundant: false
  }
}

output managedEnvironmentId string = managedEnvironment.id
