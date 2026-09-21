// =====================================================================
// VertexBPMN Azure deployment — root orchestration (P5)
// Versioned Bicep IaC. One main per environment via .bicepparam files.
// Non-secret params only; secrets come from Key Vault by reference.
// =====================================================================
targetScope = 'resourceGroup'

@description('Unique deployment environment, e.g. dev | stage | prod. Drives resource names and sku.')
@minLength(2)
@maxLength(10)
param environment string

@description('Primary Azure location (all resources unless overridden).')
param location string = resourceGroup().location

@description('Location for PostgreSQL Flexible Server (region-restricted on this subscription).')
param postgresLocation string = location

@description('Resource name prefix (short, lowercase). Default derived as vertexbpmn-<environment>.')
param namePrefix string

@description('PostgreSQL admin username (non-secret; password lives in Key Vault as pg-admin-password).')
param postgresAdminUsername string = 'pgadmin'

@description('PostgreSQL admin password. SECURE — supply at deploy time via a Key Vault reference (never in repo or parameterfile). Empty = server created with placeholder (deploy-time reference recommended).')
@secure()
param postgresAdminPassword string = ''

@description('Optional VM public IP to allow in the Postgres firewall for acceptance tests (empty = no rule).')
@minLength(0)
param allowVmTestIp string = ''

@description('Studio client-side API base URL (public HTTPS origin).')
param studioApiBaseUrl string = ''

@description('OIDC authority (empty = in-memory/local profile).')
param oidcAuthority string = ''
param oidcClientId string = ''
param oidcApiScope string = ''

@description('Initial Studio replica count (>=1; keep 1 until P4 session acceptance).')
@minValue(1)
param studioReplicas int = 1

@description('Container image tags. Separate from versions for local staging.')
param imageTag string = 'latest'

// ---- naming helpers ----
var prefix = empty(namePrefix) ? 'vertexbpmn-${environment}' : namePrefix
var laWorkspaceName = '${prefix}-la'
var acrName = replace('${prefix}acr', '-', '')
var kvName = '${prefix}kv'
var vaultUri = 'https://${kvName}.vault.azure.net/'
var saName = take(replace('${prefix}sa', '-', ''), 24)
var psqlName = '${prefix}psql'
var sbName = '${prefix}sb'
var aacName = '${prefix}aca'

// =====================================================================
// 1. NETWORK — VNet, deleg. subnet for ACA, private endpoints + DNS zones
// =====================================================================
module network './modules/network.bicep' = {
  name: '${deployment().name}-network'
  params: {
    vnetName: '${prefix}-vnet'
    location: location
    environment: environment
    postgresId: postgres.outputs.flexibleServerId
    postgresPrivateDnsZoneName: '${psqlName}.postgres.database.azure.com'
    kvId: keyvault.outputs.id
    kvPrivateDnsZoneName: 'privatelink.vaultcore.azure.net'
    sbId: servicebus.outputs.namespaceId
    sbPrivateDnsZoneName: 'privatelink.servicebus.windows.net'
    acrId: acr.outputs.registryId
    acrPrivateDnsZoneName: 'privatelink.azurecr.io'
    caName: aacName
  }
}

// =====================================================================
// 2. OBSERVABILITY — Log Analytics + App Insights
// =====================================================================
module observability './modules/observability.bicep' = {
  name: '${deployment().name}-obs'
  params: {
    location: location
    laWorkspaceName: laWorkspaceName
    appInsightsName: '${prefix}-ai'
    environment: environment
  }
}

// =====================================================================
// 3. MESSAGING — Azure Service Bus namespace, topic, subscriptions
// =====================================================================
module servicebus './modules/servicebus.bicep' = {
  name: '${deployment().name}-sb'
  params: {
    location: location
    namespaceName: sbName
    environment: environment
  }
}

// =====================================================================
// 4. DATA — PostgreSQL Flexible Server (5 engine DBs) + Key Vault
//    + Storage (Data Protection key-ring blobs)
// =====================================================================
module postgres './modules/postgresql.bicep' = {
  name: '${deployment().name}-pg'
  params: {
    location: postgresLocation
    serverName: psqlName
    environment: environment
    adminUsername: postgresAdminUsername
    adminPassword: postgresAdminPassword
    allowVmTestIp: allowVmTestIp
  }
}

module keyvault './modules/keyvault.bicep' = {
  name: '${deployment().name}-kv'
  params: {
    location: location
    name: kvName
    environment: environment
  }
}

module storage './modules/storage.bicep' = {
  name: '${deployment().name}-sa'
  params: {
    location: location
    name: saName
    environment: environment
  }
}

// =====================================================================
// 5. CONTAINER REGISTRY
// =====================================================================
module acr './modules/acr.bicep' = {
  name: '${deployment().name}-acr'
  params: {
    location: location
    name: acrName
    environment: environment
  }
}

// =====================================================================
// 6. CONTAINER APPS ENVIRONMENT (VNet-integrated) + APPS + MIGRATION JOB
// =====================================================================
module containerAppsEnvironment './modules/aca-environment.bicep' = {
  name: '${deployment().name}-acaenv'
  params: {
    location: location
    name: aacName
    environment: environment
    acaSubnetId: network.outputs.acaSubnetId
  }
  dependsOn: [
    network
  ]
}

module studio './modules/containerapp.bicep' = {
  name: '${deployment().name}-studio'
  params: {
    location: location
    environment: environment
    name: '${prefix}-studio'
    managedEnvironmentId: containerAppsEnvironment.outputs.managedEnvironmentId
    imageName: '${acr.outputs.registryLoginServer}/vertexbpmn-studio:${imageTag}'
    registryServer: acr.outputs.registryLoginServer
    containerPort: 8080
    ingress: 'external'
    targetPort: 8080
    appMiClientId: identity.outputs.appMiClientId
    appMiId: identity.outputs.appMiResourceId
    apiBaseUrl: studioApiBaseUrl
    oidcAuthority: oidcAuthority
    oidcClientId: oidcClientId
    oidcApiScope: oidcApiScope
    dataProtectionBlobUri: storage.outputs.dataprotectionStudioBlobUri
    dataProtectionKeyId: keyvault.outputs.dataprotectionKeyId
    sessionStoreConnectionStringSecretName: 'oidc-session-store-connectionstring'
    keyVaultName: kvName
    keyVaultUri: vaultUri
    jwtSecretKeySecretName: 'jwt-secret-key'
    jwtAudience: 'vertexbpmn-api'
    applyMigrationsOnStartup: false
    replicas: studioReplicas
    minReplicas: 1
  }
  dependsOn: [
    containerAppsEnvironment
    keyvault
    storage
  ]
}

module api './modules/containerapp.bicep' = {
  name: '${deployment().name}-api'
  params: {
    location: location
    environment: environment
    name: '${prefix}-api'
    managedEnvironmentId: containerAppsEnvironment.outputs.managedEnvironmentId
    imageName: '${acr.outputs.registryLoginServer}/vertexbpmn-api:${imageTag}'
    registryServer: acr.outputs.registryLoginServer
    containerPort: 8080
    ingress: 'internal'
    targetPort: 8080
    appMiClientId: identity.outputs.appMiClientId
    appMiId: identity.outputs.appMiResourceId
    dataProtectionBlobUri: storage.outputs.dataprotectionApiBlobUri
    dataProtectionKeyId: keyvault.outputs.dataprotectionKeyId
    serviceBusFullyQualifiedNamespace: servicebus.outputs.fullyQualifiedNamespace
    serviceBusTopicName: servicebus.outputs.topicName
    serviceBusSubscriptionName: servicebus.outputs.apiSubscriptionName
    keyVaultName: kvName
    keyVaultUri: vaultUri
    jwtSecretKeySecretName: 'jwt-secret-key'
    jwtAudience: 'vertexbpmn-api'
    connectionSecretRefs: [
      'BpmnDbContext:secretref:pg-bpmn'
      'TenantDbContext:secretref:pg-tenants'
      'SimulationScenarioDbContext:secretref:pg-simulation'
      'ProcessMiningEvents:secretref:pg-processminingevents'
      'DecisionDbContext:secretref:pg-decision'
      'DependencyRegistry:secretref:pg-registry'
    ]
    applyMigrationsOnStartup: false
    replicas: 1
    minReplicas: 1
  }
  dependsOn: [
    containerAppsEnvironment
    keyvault
    storage
    servicebus
  ]
}

module agentWorker './modules/containerapp.bicep' = {
  name: '${deployment().name}-agent'
  params: {
    location: location
    environment: environment
    name: '${prefix}-agent-worker'
    managedEnvironmentId: containerAppsEnvironment.outputs.managedEnvironmentId
    imageName: '${acr.outputs.registryLoginServer}/vertexbpmn-agent-worker:${imageTag}'
    registryServer: acr.outputs.registryLoginServer
    containerPort: 8080
    ingress: 'internal'
    targetPort: 8080
    appMiClientId: identity.outputs.appMiClientId
    appMiId: identity.outputs.appMiResourceId
    applyMigrationsOnStartup: false
    replicas: 1
    minReplicas: 0
  }
  dependsOn: [
    containerAppsEnvironment
  ]
}

// Migration job — manual start, exactly one instance
module migrationJob './modules/migration-job.bicep' = {
  name: '${deployment().name}-migrate'
  params: {
    location: location
    environment: environment
    name: '${prefix}-migrate'
    managedEnvironmentId: containerAppsEnvironment.outputs.managedEnvironmentId
    imageName: '${acr.outputs.registryLoginServer}/vertexbpmn-api:${imageTag}'
    registryServer: acr.outputs.registryLoginServer
    appMiClientId: identity.outputs.appMiClientId
    appMiId: identity.outputs.appMiResourceId
    keyVaultName: kvName
    keyVaultUri: vaultUri
    serviceBusFullyQualifiedNamespace: servicebus.outputs.fullyQualifiedNamespace
    serviceBusTopicName: servicebus.outputs.topicName
    serviceBusSubscriptionName: servicebus.outputs.apiSubscriptionName
    dataProtectionBlobUri: storage.outputs.dataprotectionApiBlobUri
    dataProtectionKeyId: keyvault.outputs.dataprotectionKeyId
    jwtSecretKeySecretName: 'jwt-secret-key'
    jwtAudience: 'vertexbpmn-api'
    connectionSecretRefs: [
      'BpmnDbContext:secretref:pg-bpmn'
      'TenantDbContext:secretref:pg-tenants'
      'SimulationScenarioDbContext:secretref:pg-simulation'
      'ProcessMiningEvents:secretref:pg-processminingevents'
      'DecisionDbContext:secretref:pg-decision'
      'DependencyRegistry:secretref:pg-registry'
    ]
  }
  dependsOn: [
    containerAppsEnvironment
  ]
}

// =====================================================================
// 7. IDENTITIES + RBAC (roles granted via Bicep, no Graph dependency)
// =====================================================================
module identity './modules/identity.bicep' = {
  name: '${deployment().name}-identity'
  params: {
    location: location
    prefix: prefix
    environment: environment
    acrId: acr.outputs.registryId
    kvId: keyvault.outputs.id
    sbNamespaceId: servicebus.outputs.namespaceId
    storageAccountId: storage.outputs.id
  }
  dependsOn: [
    acr
    keyvault
    servicebus
    storage
  ]
}

// =====================================================================
// Root outputs (non-secret)
// =====================================================================
output environment string = environment
output prefix string = prefix
output keyVaultUri string = vaultUri
output keyVaultName string = kvName
output storageAccountName string = saName
output postgresServerName string = psqlName
output postgresFqdn string = postgres.outputs.fqdn
output serviceBusNamespace string = sbName
output acrLoginServer string = acr.outputs.registryLoginServer
output managedEnvironmentId string = containerAppsEnvironment.outputs.managedEnvironmentId
output studioUrl string = studio.outputs.url
output appIdentityClientId string = identity.outputs.appMiClientId
