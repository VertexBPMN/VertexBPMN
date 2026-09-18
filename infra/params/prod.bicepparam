// Production environment parameterfile — non-secret values only.
// Secrets are referenced from Key Vault; never place credentials here.
using '../main.bicep'

param environment = 'prod'
param location = 'francecentral'
param postgresLocation = 'francecentral'
param namePrefix = 'vertexbpmn-prod'
param postgresAdminUsername = 'vbpnadmin'
param imageTag = '1.0.0'
param studioApiBaseUrl = 'https://studio.vertexbpmn.app'
param oidcAuthority = ''
param oidcClientId = ''
param oidcApiScope = ''
param studioReplicas = 1
