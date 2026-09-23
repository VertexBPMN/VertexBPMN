// Stage environment parameter file — GENERIC TEMPLATE, non-sensitive values only.
// Open-source repo: no real resource/tenant identifiers here.
// To deploy your own Stage environment:
//   1. cp params/stage.bicepparam params/stage.local.bicepparam
//   2. Fill in YOUR values for namePrefix, location, postgresAdminUsername, etc.
//   3. Deploy with: az deployment group create -g <your-rg> -f main.bicep \
//        -p params/stage.local.bicepparam
// Secrets (PostgreSQL admin password, OIDC client secret, session-store
// connection string) live in Key Vault as secret references — never here.
using '../main.bicep'

param environment = 'stage'
param location = '<your-region>'            // e.g. '<your-region>'
param postgresLocation = '<your-region>'    // e.g. '<your-region>'
param namePrefix = '<your-name-prefix>'     // e.g. '<your-name-prefix>'
param postgresAdminUsername = '<your-db-admin>'
param imageTag = '1.0.0'
param studioApiBaseUrl = ''
param oidcAuthority = ''
param oidcClientId = ''
param oidcApiScope = ''
param studioReplicas = 1
