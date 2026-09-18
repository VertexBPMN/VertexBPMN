@description('The Azure region/location where the resources will be deployed. Defaults to the resource group location.')
param location string = resourceGroup().location

@description('The name of the storage account. Must be globally unique, 3-24 lowercase alphanumeric characters.')
param name string

@description('The environment name used for tagging, e.g. dev, staging, prod.')
param environment string

// ---------------------------------------------------------------------------
// Storage account
// ---------------------------------------------------------------------------
resource storageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: name
  location: location
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  tags: {
    Environment: environment
  }
  properties: {
    supportsHttpsTrafficOnly: true
    minimumTlsVersion: 'TLS1_2'
    allowBlobPublicAccess: false
    networkAcls: {
      defaultAction: 'Allow'
      bypass: 'AzureServices'
    }
  }
}

// ---------------------------------------------------------------------------
// Blob service
// ---------------------------------------------------------------------------
resource blobService 'Microsoft.Storage/storageAccounts/blobServices@2023-05-01' = {
  name: 'default'
  parent: storageAccount
  properties: {}
}

resource apisContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  name: 'dataprotection-api'
  parent: blobService
  properties: {
    publicAccess: 'None'
    defaultEncryptionScope: '$account-encryption-key'
  }
}

resource studioContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  name: 'dataprotection-studio'
  parent: blobService
  properties: {
    publicAccess: 'None'
    defaultEncryptionScope: '$account-encryption-key'
  }
}

// ---------------------------------------------------------------------------
// Outputs
// ---------------------------------------------------------------------------
output id string = storageAccount.id
output dataprotectionApiBlobUri string = 'https://${name}.blob.core.windows.net/dataprotection-api'
output dataprotectionStudioBlobUri string = 'https://${name}.blob.core.windows.net/dataprotection-studio'
