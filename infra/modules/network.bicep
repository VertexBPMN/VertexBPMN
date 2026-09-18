@description('Name of the virtual network.')
param vnetName string

@description('Location for all resources in this module.')
param location string

@description('Deployment environment label (e.g. dev, staging, prod).')
param environment string

@description('Resource ID of the Azure PostgreSQL Flexible Server endpoint.')
param postgresId string

@description('Name of the private DNS zone for PostgreSQL.')
param postgresPrivateDnsZoneName string

@description('Resource ID of the Azure Key Vault endpoint.')
param kvId string

@description('Name of the private DNS zone for Key Vault.')
param kvPrivateDnsZoneName string

@description('Resource ID of the Azure Service Bus namespace endpoint.')
param sbId string

@description('Name of the private DNS zone for Service Bus.')
param sbPrivateDnsZoneName string

@description('Resource ID of the Azure Container Registry endpoint.')
param acrId string

@description('Name of the private DNS zone for ACR.')
param acrPrivateDnsZoneName string

@description('Name of the Container Apps environment.')
param caName string

// ---------------------------------------------------------------------------
// Virtual network with delegated ACA subnet and a private-endpoints subnet
// ---------------------------------------------------------------------------
resource vnet 'Microsoft.Network/virtualNetworks@2023-11-01' = {
  name: vnetName
  location: location
  tags: {
    Environment: environment
  }
  properties: {
    addressSpace: {
      addressPrefixes: [
        '10.10.0.0/16'
      ]
    }
    subnets: [
      {
        name: 'aca-subnet'
        properties: {
          addressPrefix: '10.10.1.0/24'
          delegations: [
            {
              name: 'aca-infra-delegation'
              properties: {
                serviceName: 'Microsoft.App/environments'
              }
            }
          ]
        }
      }
      {
        name: 'private-endpoints-subnet'
        properties: {
          addressPrefix: '10.10.2.0/24'
          privateEndpointNetworkPolicies: 'Disabled'
        }
      }
    ]
  }
}

resource acaSubnet 'Microsoft.Network/virtualNetworks/subnets@2023-11-01' = {
  parent: vnet
  name: 'aca-subnet'
}

resource privateEndpointsSubnet 'Microsoft.Network/virtualNetworks/subnets@2023-11-01' = {
  parent: vnet
  name: 'private-endpoints-subnet'
}

#disable-next-line BCP081 // serviceAssociationLinks has no published types for this API version
resource acaServiceAssociationLink 'Microsoft.Network/virtualNetworks/subnets/serviceAssociationLinks@2023-11-01' = {
  parent: acaSubnet
  name: 'aca-service-association'
  properties: {
    linkedResourceType: 'Microsoft.App/environments'
    link: '${subscription().id}/resourceGroups/${resourceGroup().name}/providers/Microsoft.App/environments/${caName}'
  }
}

// ---------------------------------------------------------------------------
// Private DNS zones
// ---------------------------------------------------------------------------
resource postgresDnsZone 'Microsoft.Network/privateDnsZones@2020-06-01' = {
  name: postgresPrivateDnsZoneName
  location: 'global'
  tags: {
    Environment: environment
  }
}

resource kvDnsZone 'Microsoft.Network/privateDnsZones@2020-06-01' = {
  name: kvPrivateDnsZoneName
  location: 'global'
  tags: {
    Environment: environment
  }
}

resource sbDnsZone 'Microsoft.Network/privateDnsZones@2020-06-01' = {
  name: sbPrivateDnsZoneName
  location: 'global'
  tags: {
    Environment: environment
  }
}

resource acrDnsZone 'Microsoft.Network/privateDnsZones@2020-06-01' = {
  name: acrPrivateDnsZoneName
  location: 'global'
  tags: {
    Environment: environment
  }
}

resource postgresDnsLink 'Microsoft.Network/privateDnsZones/virtualNetworkLinks@2020-06-01' = {
  parent: postgresDnsZone
  name: '${vnetName}-postgres-link'
  location: 'global'
  tags: {
    Environment: environment
  }
  properties: {
    virtualNetwork: {
      id: vnet.id
    }
    registrationEnabled: false
  }
}

resource kvDnsLink 'Microsoft.Network/privateDnsZones/virtualNetworkLinks@2020-06-01' = {
  parent: kvDnsZone
  name: '${vnetName}-kv-link'
  location: 'global'
  tags: {
    Environment: environment
  }
  properties: {
    virtualNetwork: {
      id: vnet.id
    }
    registrationEnabled: false
  }
}

resource sbDnsLink 'Microsoft.Network/privateDnsZones/virtualNetworkLinks@2020-06-01' = {
  parent: sbDnsZone
  name: '${vnetName}-sb-link'
  location: 'global'
  tags: {
    Environment: environment
  }
  properties: {
    virtualNetwork: {
      id: vnet.id
    }
    registrationEnabled: false
  }
}

resource acrDnsLink 'Microsoft.Network/privateDnsZones/virtualNetworkLinks@2020-06-01' = {
  parent: acrDnsZone
  name: '${vnetName}-acr-link'
  location: 'global'
  tags: {
    Environment: environment
  }
  properties: {
    virtualNetwork: {
      id: vnet.id
    }
    registrationEnabled: false
  }
}

// ---------------------------------------------------------------------------
// Private endpoints + private DNS zone groups
// ---------------------------------------------------------------------------
resource postgresPe 'Microsoft.Network/privateEndpoints@2023-04-01' = {
  name: 'pe-${postgresPrivateDnsZoneName}'
  location: location
  tags: {
    Environment: environment
  }
  properties: {
    subnet: {
      id: privateEndpointsSubnet.id
    }
    privateLinkServiceConnections: [
      {
        name: 'pls-${postgresPrivateDnsZoneName}'
        properties: {
          privateLinkServiceId: postgresId
          groupIds: [
            'postgresqlServer'
          ]
        }
      }
    ]
  }
}

resource postgresPeDnsZoneGroup 'Microsoft.Network/privateEndpoints/privateDnsZoneGroups@2023-04-01' = {
  parent: postgresPe
  name: 'postgres-dns-zone-group'
  properties: {
    privateDnsZoneConfigs: [
      {
        name: 'postgres-dns-config'
        properties: {
          privateDnsZoneId: postgresDnsZone.id
        }
      }
    ]
  }
}

resource kvPe 'Microsoft.Network/privateEndpoints@2023-04-01' = {
  name: 'pe-${kvPrivateDnsZoneName}'
  location: location
  tags: {
    Environment: environment
  }
  properties: {
    subnet: {
      id: privateEndpointsSubnet.id
    }
    privateLinkServiceConnections: [
      {
        name: 'pls-${kvPrivateDnsZoneName}'
        properties: {
          privateLinkServiceId: kvId
          groupIds: [
            'vault'
          ]
        }
      }
    ]
  }
}

resource kvPeDnsZoneGroup 'Microsoft.Network/privateEndpoints/privateDnsZoneGroups@2023-04-01' = {
  parent: kvPe
  name: 'kv-dns-zone-group'
  properties: {
    privateDnsZoneConfigs: [
      {
        name: 'kv-dns-config'
        properties: {
          privateDnsZoneId: kvDnsZone.id
        }
      }
    ]
  }
}

resource sbPe 'Microsoft.Network/privateEndpoints@2023-04-01' = {
  name: 'pe-${sbPrivateDnsZoneName}'
  location: location
  tags: {
    Environment: environment
  }
  properties: {
    subnet: {
      id: privateEndpointsSubnet.id
    }
    privateLinkServiceConnections: [
      {
        name: 'pls-${sbPrivateDnsZoneName}'
        properties: {
          privateLinkServiceId: sbId
          groupIds: [
            'namespace'
          ]
        }
      }
    ]
  }
}

resource sbPeDnsZoneGroup 'Microsoft.Network/privateEndpoints/privateDnsZoneGroups@2023-04-01' = {
  parent: sbPe
  name: 'sb-dns-zone-group'
  properties: {
    privateDnsZoneConfigs: [
      {
        name: 'sb-dns-config'
        properties: {
          privateDnsZoneId: sbDnsZone.id
        }
      }
    ]
  }
}

resource acrPe 'Microsoft.Network/privateEndpoints@2023-04-01' = {
  name: 'pe-${acrPrivateDnsZoneName}'
  location: location
  tags: {
    Environment: environment
  }
  properties: {
    subnet: {
      id: privateEndpointsSubnet.id
    }
    privateLinkServiceConnections: [
      {
        name: 'pls-${acrPrivateDnsZoneName}'
        properties: {
          privateLinkServiceId: acrId
          groupIds: [
            'registry'
          ]
        }
      }
    ]
  }
}

resource acrPeDnsZoneGroup 'Microsoft.Network/privateEndpoints/privateDnsZoneGroups@2023-04-01' = {
  parent: acrPe
  name: 'acr-dns-zone-group'
  properties: {
    privateDnsZoneConfigs: [
      {
        name: 'acr-dns-config'
        properties: {
          privateDnsZoneId: acrDnsZone.id
        }
      }
    ]
  }
}

// ---------------------------------------------------------------------------
// Outputs
// ---------------------------------------------------------------------------
output vnetId string = vnet.id

output acaSubnetId string = acaSubnet.id
