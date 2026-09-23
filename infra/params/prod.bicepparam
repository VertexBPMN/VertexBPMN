// Production environment parameter file — GENERIC TEMPLATE, non-sensitive values only.
// Open-source repo: no real resource/tenant identifiers here.
// To deploy Production: copy to params/prod.local.bicepparam, fill in YOUR values,
// and deploy with that file. See stage.bicepparam header for the workflow.
// Secrets are referenced from Key Vault; never place credentials here.
using '../main.bicep'

param environment = 'prod'
param location = '<your-region>'
param postgresLocation = '<your-region>'
param namePrefix = '<your-name-prefix>'
param postgresAdminUsername = '<your-db-admin>'
param imageTag = '1.0.0'
param studioApiBaseUrl = 'https://studio.<your-domain>'
param oidcAuthority = ''
param oidcClientId = ''
param oidcApiScope = ''
param studioReplicas = 1
