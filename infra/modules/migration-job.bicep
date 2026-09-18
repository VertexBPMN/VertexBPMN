@description('Resource group/region where the job is deployed.')
param location string

@description('Deployment environment (e.g. dev, staging, prod).')
param environment string

@description('Name of the migration job.')
param name string

@description('Resource id of the Container Apps environment.')
param managedEnvironmentId string

@description('Full image name including tag, e.g. myregistry.azurecr.io/app:migrate.')
param imageName string

@description('Container registry server, e.g. myregistry.azurecr.io.')
param registryServer string

@description('Client id of the user-assigned managed identity used by the app.')
param appMiClientId string

@description('Resource id of the user-assigned managed identity. Defaults to empty; if empty, no user-assigned identity is attached.')
param appMiId string = ''

resource migrationJob 'Microsoft.App/jobs@2024-02-02-preview' = {
  name: name
  location: location
  tags: {
    environment: environment
  }
  identity: appMiId != '' ? {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${appMiId}': {}
    }
  } : {
    type: 'SystemAssigned'
  }
  properties: {
    environmentId: managedEnvironmentId
    configuration: {
      triggerType: 'Manual'
      replicaRetryLimit: 2
      replicaTimeout: 3600
      registries: [
        {
          server: registryServer
          identity: appMiId != '' ? '${appMiId}/userAssignedIdentities/${appMiClientId}' : ''
        }
      ]
    }
    template: {
      containers: [
        {
          image: imageName
          name: name
          resources: {
            cpu: '0.25'
            memory: '0.5Gi'
          }
          args: [
            '--migrate-only'
          ]
        }
      ]
    }
  }
}

output id string = migrationJob.id
output name string = migrationJob.name
