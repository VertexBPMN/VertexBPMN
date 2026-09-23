// =====================================================================
// VertexBPMN identity + RBAC module (P5)
// Creates the user-assigned managed identity for the apps and grants
// the built-in roles it needs (ACR Pull, Key Vault secrets/crypto,
// Service Bus sender/receiver, Storage Blob Data Contributor).
// Roles are granted via Bicep role assignments (no Graph dependency).
// =====================================================================
targetScope = 'resourceGroup'

@description('Primary Azure location for the identity.')
param location string

@description('Resource name prefix. The identity is named <prefix>-mi.')
param prefix string

@description('Deployment environment (dev | stage | prod); stamped onto tags.')
param environment string

@description('Container registry resource ID (ACR Pull is granted here).')
param acrId string

@description('Key Vault resource ID (Secrets User + Crypto User roles).')
param kvId string

@description('Service Bus namespace resource ID (Data Sender + Data Receiver roles).')
param sbNamespaceId string

@description('Storage account resource ID (Blob Data Contributor role).')
param storageAccountId string

// ---- built-in role definition GUIDs (ids embedded as strings) ----
var acrPullRoleId = '7f951dda-4ed3-4680-a7ca-43fe172d538d'
var kvSecretsUserRoleId = '4633458b-17de-408a-b874-0445c86b69e6'
var kvCryptoUserRoleId = '12338af0-0e69-4776-bea7-57ae8d297424'
var sbDataSenderRoleId = '69a216fc-b8fb-44d8-bc22-1f3c2cd27a39'
var sbDataReceiverRoleId = '4f6d3b9b-027b-4f4c-9142-0e5a2a2247e0'
var blobDataContributorRoleId = 'ba92f5b4-2d11-453d-a403-e96b0029c9fe'

// Full roleDefinitionId ARM path built from the current subscription.
// e.g. /subscriptions/<sub>/providers/Microsoft.Authorization/roleDefinitions/<guid>
var roleDefinitionsRoot = '${subscription().id}/providers/Microsoft.Authorization/roleDefinitions'

// ---- existing resource references (resolved from IDs passed in) ----
resource acr 'Microsoft.ContainerRegistry/registries@2023-01-01-preview' existing = {
  name: last(split(acrId, '/'))
}

resource kv 'Microsoft.KeyVault/vaults@2023-07-01' existing = {
  name: last(split(kvId, '/'))
}

resource sb 'Microsoft.ServiceBus/namespaces@2022-10-01-preview' existing = {
  name: last(split(sbNamespaceId, '/'))
}

resource storage 'Microsoft.Storage/storageAccounts@2023-05-01' existing = {
  name: last(split(storageAccountId, '/'))
}

// ---- user-assigned managed identity ----
resource identity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: '${prefix}-mi'
  location: location
  tags: {
    Environment: environment
  }
}

// ---- role assignments (principalId = created identity's principalId) ----
// Note: role assignment `name` must be stable before deployment, so it is
// derived from resource + role GUIDs only (not identity principalId).

// ACR Pull on the container registry
resource acrPullRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(acrId, acrPullRoleId, 'acrpull')
  scope: acr
  properties: {
    roleDefinitionId: '${roleDefinitionsRoot}/${acrPullRoleId}'
    principalId: identity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

// Key Vault Secrets User on the vault
resource kvSecretsUserRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(kvId, kvSecretsUserRoleId, 'kvsecrets')
  scope: kv
  properties: {
    roleDefinitionId: '${roleDefinitionsRoot}/${kvSecretsUserRoleId}'
    principalId: identity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

// Key Vault Crypto User on the vault (Data Protection asymmetric keys)
resource kvCryptoUserRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(kvId, kvCryptoUserRoleId, 'kvcrypto')
  scope: kv
  properties: {
    roleDefinitionId: '${roleDefinitionsRoot}/${kvCryptoUserRoleId}'
    principalId: identity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

// Service Bus Data Sender on the namespace
resource sbDataSenderRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(sbNamespaceId, sbDataSenderRoleId, 'sbsender')
  scope: sb
  properties: {
    roleDefinitionId: '${roleDefinitionsRoot}/${sbDataSenderRoleId}'
    principalId: identity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

// Service Bus Data Receiver on the namespace
resource sbDataReceiverRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(sbNamespaceId, sbDataReceiverRoleId, 'sbreceiver')
  scope: sb
  properties: {
    roleDefinitionId: '${roleDefinitionsRoot}/${sbDataReceiverRoleId}'
    principalId: identity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

// Storage Blob Data Contributor on the storage account (Data Protection key ring)
resource sbBlobDataContributorRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storageAccountId, blobDataContributorRoleId, 'sbcontrib')
  scope: storage
  properties: {
    roleDefinitionId: '${roleDefinitionsRoot}/${blobDataContributorRoleId}'
    principalId: identity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

// ---- outputs ----
output appMiClientId string = identity.properties.clientId
output appMiResourceId string = identity.id
