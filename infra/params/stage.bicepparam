// Stage environment parameterfile — non-secret values only.
// Secrets: PostgreSQL admin password lives in Key Vault (pg-admin-password),
// OIDC client secret + session store connection string are set as Key Vault
// secret references at deploy time, never in this file.
using '../main.bicep'

param environment = 'stage'
param location = '<your-region>'
param postgresLocation = '<your-region>'
param namePrefix = '<your-name-prefix>'
param postgresAdminUsername = '<db-admin>'
param imageTag = '1.0.0'
param studioApiBaseUrl = ''
param oidcAuthority = ''
param oidcClientId = ''
param oidcApiScope = ''
param studioReplicas = 1
