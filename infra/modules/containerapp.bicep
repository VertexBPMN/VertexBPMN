// =====================================================================
// VertexBPMN generic container app (studio/api/agent-worker)
// =====================================================================
targetScope = 'resourceGroup'

param location string
param environment string
param name string
param managedEnvironmentId string
param imageName string
@description('ACR login server; the app identity must hold ACR Pull.')
param registryServer string
param containerPort int = 8080
@allowed([
  'external'
  'internal'
])
@description('external = public HTTPS ingress; internal = VNet-only.')
param ingress string
param targetPort int = 8080

@description('User-assigned managed identity RESOURCE ID assigned to the app (empty = system-assigned only).')
param appMiId string = ''

@description('User-assigned managed identity CLIENT ID, exported as AZURE_CLIENT_ID so DefaultAzureCredential resolves this MI (empty = rely on system-assigned).')
param appMiClientId string = ''

// ---- app-specific configuration (all optional; empty = not set) ----
param apiBaseUrl string = ''
param oidcAuthority string = ''
param oidcClientId string = ''
param oidcApiScope string = ''
param dataProtectionBlobUri string = ''
param dataProtectionKeyId string = ''
param sessionStoreConnectionStringSecretName string = ''
@description('Azure Service Bus namespace FQDN for outbox/inbox (P1). Empty = outbox disabled.')
param serviceBusFullyQualifiedNamespace string = ''
param serviceBusTopicName string = ''
param serviceBusSubscriptionName string = ''
param applyMigrationsOnStartup bool = false
param replicas int = 1
param minReplicas int = 1
@description('CPU in vCPU (ACA consumption).')
param cpu string = '0.5'
@description('Memory like 1.0Gi.')
param memory string = '1.0Gi'

@description('Engine connection-string secret references, e.g. BpmnDbContext:secretref:pg-bpmn. Values live in Key Vault, never plaintext.')
param connectionSecretRefs array = []

// secretref format: <ConfigKey>:secretref:<KeyVaultSecretName>
@description('Key Vault that holds the secret references (used to build secret URLs).')
param keyVaultName string = ''

var identityType = empty(appMiId) ? 'SystemAssigned' : 'UserAssigned'
var identityId = empty(appMiId) ? null : {
  '${appMiId}': {}
}

// ---- environment variables ----
var envVars = concat([
  {
    name: 'ASPNETCORE_HTTP_PORTS'
    value: string(containerPort)
  }
  {
    name: 'Database__ApplyMigrationsOnStartup'
    value: string(applyMigrationsOnStartup)
  }
  {
    name: 'OperationalMode'
    value: (environment == 'prod') ? 'Production' : ((environment == 'stage') ? 'Stage' : 'Development')
  }
], empty(appMiClientId) ? [] : [
  {
    name: 'AZURE_CLIENT_ID'
    value: appMiClientId
  }
], empty(apiBaseUrl) ? [] : [
  {
    name: 'ApiBaseUrl'
    value: apiBaseUrl
  }
], empty(oidcAuthority) ? [] : [
  {
    name: 'StudioAuthentication__Authority'
    value: oidcAuthority
  }
  {
    name: 'StudioAuthentication__ClientId'
    value: oidcClientId
  }
  {
    name: 'StudioAuthentication__ApiScope'
    value: oidcApiScope
  }
], empty(dataProtectionBlobUri) ? [] : [
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
], empty(sessionStoreConnectionStringSecretName) ? [] : [
  {
    name: 'OidcSessionStore__Provider'
    value: 'npgsql'
  }
  {
    name: 'OidcSessionStore__ConnectionString'
    value: 'secretref:${sessionStoreConnectionStringSecretName}'
  }
], empty(serviceBusFullyQualifiedNamespace) ? [] : [
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
], connectionStringEnvVars)

var connectionStringEnvVars = [for cs in connectionSecretRefs: {
  name: 'ConnectionStrings__${split(cs, ':')[0]}'
  value: cs
}]

resource app 'Microsoft.App/containerApps@2024-03-01' = {
  name: name
  location: location
  identity: {
    type: identityType
    userAssignedIdentities: identityId
  }
  properties: {
    managedEnvironmentId: managedEnvironmentId
    configuration: {
      activeRevisionsMode: 'Single'
      secrets: [for cs in connectionSecretRefs: {
        name: split(cs, ':')[2]
        keyVaultUrl: 'https://${keyVaultName}.vault.azure.net/secrets/${split(cs, ':')[2]}'
        identity: appMiId
      }]
      ingress: {
        external: ingress == 'external'
        targetPort: targetPort
        transport: 'auto'
        allowInsecure: false
      }
      registries: [
        {
          server: registryServer
          identity: empty(appMiId) ? '' : appMiId
        }
      ]
    }
    template: {
      revisionSuffix: ''
      containers: [
        {
          image: imageName
          name: name
          resources: {
            cpu: cpu
            memory: memory
          }
          env: envVars
        }
      ]
      scale: {
        minReplicas: minReplicas
        maxReplicas: replicas
      }
    }
  }
}

output url string = ingress == 'external' ? app.properties.configuration.ingress.fqdn : ''
output name string = app.name
