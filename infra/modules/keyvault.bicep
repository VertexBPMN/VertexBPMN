param location string
param name string
param environment string

resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: name
  location: location
  tags: {
    Environment: environment
  }
  properties: {
    enableRbacAuthorization: true
    enableSoftDelete: true
    softDeleteRetentionInDays: 7
    enablePurgeProtection: false
    tenantId: subscription().tenantId
    sku: {
      family: 'A'
      name: 'standard'
    }
  }
}

resource dataprotectionKey 'Microsoft.KeyVault/vaults/keys@2023-07-01' = {
  parent: keyVault
  name: 'dataprotection-key'
  properties: {
    kty: 'RSA'
    keySize: 3072
    keyOps: [
      'wrapKey'
      'unwrapKey'
      'encrypt'
      'decrypt'
    ]
    attributes: {
      enabled: true
    }
  }
}

output id string = keyVault.id
output dataprotectionKeyId string = dataprotectionKey.id
