@description('Azure region where the Flexible Server is deployed. This subscription has Flexible Server RESTRICTED in <your-region>/westeurope, so the caller must pass a supported region such as <your-region>.')
param location string

@description('Name of the PostgreSQL Flexible Server resource.')
param serverName string

@description('Deployment environment tag value, e.g. dev, staging, production.')
param environment string

@description('Administrator login name for the PostgreSQL server.')
param adminUsername string = '<db-admin>'

@description('Administrator password. Never hardcode secrets in the repository; leave empty to supply at deployment time (e.g. via a deployment-time reference to the Key Vault secret).')
@secure()
param adminPassword string = ''

@description('Comma-separated list of public IPs to allow through the server firewall. Empty by default (no rules created).')
param allowVmTestIp string = ''

resource flexibleServer 'Microsoft.DBforPostgreSQL/flexibleServers@2023-06-01-preview' = {
  name: serverName
  location: location
  tags: {
    Environment: environment
  }
  sku: {
    name: 'Standard_B1ms'
    tier: 'Burstable'
  }
  properties: {
    version: '16'
    administratorLogin: adminUsername
    administratorLoginPassword: adminPassword
    storage: {
      storageSizeGB: 32
      tier: 'P4'
    }
    highAvailability: {
      mode: 'Disabled'
    }
    backup: {
      backupRetentionDays: 7
      geoRedundantBackup: 'Disabled'
    }
  }
}

// Create a firewall rule for each IP in the comma-separated allowVmTestIp list.
// Empty entries (split on empty string) are skipped.
resource firewallRule 'Microsoft.DBforPostgreSQL/flexibleServers/firewallRules@2023-06-01-preview' = [for ipAddress in split(allowVmTestIp, ','): if (!empty(trim(ipAddress))) {
  parent: flexibleServer
  name: 'AllowVmTest-${replace(replace(trim(ipAddress), '.', '-'), ':', '-')}'
  properties: {
    startIpAddress: trim(ipAddress)
    endIpAddress: trim(ipAddress)
  }
}]

// Database child resources.
resource dbBpmn 'Microsoft.DBforPostgreSQL/flexibleServers/databases@2023-06-01-preview' = {
  parent: flexibleServer
  name: 'bpmn'
}

resource dbTenants 'Microsoft.DBforPostgreSQL/flexibleServers/databases@2023-06-01-preview' = {
  parent: flexibleServer
  name: 'tenants'
}

resource dbSimulation 'Microsoft.DBforPostgreSQL/flexibleServers/databases@2023-06-01-preview' = {
  parent: flexibleServer
  name: 'simulation'
}

resource dbProcessMiningEvents 'Microsoft.DBforPostgreSQL/flexibleServers/databases@2023-06-01-preview' = {
  parent: flexibleServer
  name: 'processminingevents'
}

resource dbDecision 'Microsoft.DBforPostgreSQL/flexibleServers/databases@2023-06-01-preview' = {
  parent: flexibleServer
  name: 'decision'
}

output flexibleServerId string = flexibleServer.id
output fqdn string = flexibleServer.properties.fullyQualifiedDomainName
output dbNames array = ['bpmn', 'tenants', 'simulation', 'processminingevents', 'decision']
