// Production environment parameterfile — non-secret values only.
// Secrets are referenced from Key Vault; never place credentials here.
using '../main.bicep'

param environment = 'prod'
param location = '<your-region>'
param postgresLocation = '<your-region>'
param namePrefix = '<prod-name-prefix>'
param postgresAdminUsername = '<db-admin>'
param imageTag = '1.0.0'
param studioApiBaseUrl = 'https://studio.<your-domain>'
param oidcAuthority = ''
param oidcClientId = ''
param oidcApiScope = ''
param studioReplicas = 1
