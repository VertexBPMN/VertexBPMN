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

@description('Engine connection-string secret references, e.g. BpmnDbContext:secretref:pg-bpmn. Values live in Key Vault, never plaintext.')
param connectionSecretRefs array = []

// secretref format: <ConfigKey>:secretref:<KeyVaultSecretName>
@description('Key Vault that holds the secret references (used to build secret URLs).')
param keyVaultName string = ''

@description('Full Key Vault URI (https://<vault>.vault.azure.net) used by the app to resolve secretref tokens at runtime.')
param keyVaultUri string = ''

@description('Azure Service Bus namespace FQDN for outbox/inbox (P1). Empty = outbox disabled.')
param serviceBusFullyQualifiedNamespace string = ''
param serviceBusTopicName string = ''
param serviceBusSubscriptionName string = ''

@description('Data Protection blob URI (shared key ring across replicas).')
param dataProtectionBlobUri string = ''
@description('Data Protection Key Vault key identifier (encrypts at-rest key ring).')
param dataProtectionKeyId string = ''

@description('Key Vault secret name holding the symmetric JWT signing key (Stage before P6 OIDC). Empty = not configured.')
param jwtSecretKeySecretName string = ''
@description('JWT audience for Stage (before P6 OIDC). Empty = not configured.')
param jwtAudience string = ''

var jwtEnvVars = empty(jwtSecretKeySecretName) ? [] : [
  {
    name: 'Jwt__SecretKey'
    value: 'secretref:${jwtSecretKeySecretName}'
  }
  {
    name: 'Jwt__Audience'
    value: jwtAudience
  }
]

var dataProtectionEnvVars = empty(dataProtectionBlobUri) ? [] : [
  {
    name: 'DataProtection__Provider'
    value: 'AzureBlobKeyVault'
  }
  {
    name: 'DataProtection__BlobUri'
    value: dataProtectionBlobUri
  }
  {
    name: 'DataProtection__KeyVaultKeyIdentifier'
    value: dataProtectionKeyId
  }
]

var serviceBusEnvVars = empty(serviceBusFullyQualifiedNamespace) ? [] : [
  {
    name: 'Runtime__Outbox__Enabled'
    value: 'true'
  }
  {
    name: 'Runtime__Outbox__Provider'
    value: 'AzureServiceBus'
  }
  {
    name: 'Runtime__Outbox__FullyQualifiedNamespace'
    value: serviceBusFullyQualifiedNamespace
  }
  {
    name: 'Runtime__Outbox__EntityName'
    value: serviceBusTopicName
  }
  {
    name: 'Runtime__Outbox__EntityType'
    value: 'Topic'
  }
  {
    name: 'Runtime__Outbox__AuthenticationMode'
    value: 'ManagedIdentity'
  }
  {
    name: 'Runtime__Outbox__ManagedIdentityClientId'
    value: appMiClientId
  }
  {
    name: 'Runtime__Inbox__Enabled'
    value: 'true'
  }
  {
    name: 'Runtime__Inbox__Subscription'
    value: serviceBusSubscriptionName
  }
  {
    name: 'Runtime__Inbox__FullyQualifiedNamespace'
    value: serviceBusFullyQualifiedNamespace
  }
  {
    name: 'Runtime__Inbox__ManagedIdentityClientId'
    value: appMiClientId
  }
]

var connectionStringEnvVars = [for cs in connectionSecretRefs: {
  name: 'ConnectionStrings__${split(cs, ':')[0]}'
  value: 'secretref:${split(cs, ':')[2]}'
}]

var containerEnvVars = concat(
  [
    {
      name: 'OperationalMode'
      value: (environment == 'prod') ? 'Production' : ((environment == 'stage') ? 'Stage' : 'Development')
    }
    {
      name: 'KeyVault__Uri'
      value: keyVaultUri
    }
  ],
  empty(appMiClientId) ? [] : [
    {
      name: 'AZURE_CLIENT_ID'
      value: appMiClientId
    }
  ],
  dataProtectionEnvVars,
  serviceBusEnvVars,
  jwtEnvVars,
  connectionStringEnvVars
)

var dbSecrets = [for cs in connectionSecretRefs: {
  name: split(cs, ':')[2]
  keyVaultUrl: 'https://${keyVaultName}.vault.azure.net/secrets/${split(cs, ':')[2]}'
}]

var jwtSecrets = empty(jwtSecretKeySecretName) ? [] : [{
  name: jwtSecretKeySecretName
  keyVaultUrl: 'https://${keyVaultName}.vault.azure.net/secrets/${jwtSecretKeySecretName}'
}]

var jobSecrets = concat(dbSecrets, jwtSecrets)

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
      secrets: [for s in jobSecrets: {
        name: s.name
        keyVaultUrl: s.keyVaultUrl
        identity: appMiId
      }]
      registries: [
        {
          server: registryServer
          identity: appMiId != '' ? appMiId : ''
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
          env: containerEnvVars
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
