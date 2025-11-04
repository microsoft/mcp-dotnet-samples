param virtualNetworkName string
param subnetName string
@description('Specifies the storage account resource name')
param resourceName string
param location string = resourceGroup().location
param tags object = {}
param enableBlob bool = true
param enableQueue bool = false
param enableTable bool = false

resource vnet 'Microsoft.Network/virtualNetworks@2021-08-01' existing = {
  name: virtualNetworkName
}

resource storageAccount 'Microsoft.Storage/storageAccounts@2021-09-01' existing = {
  name: resourceName
}

// Storage DNS zone names
var blobPrivateDNSZoneName = 'privatelink.blob.${environment().suffixes.storage}'
var queuePrivateDNSZoneName = 'privatelink.queue.${environment().suffixes.storage}'
var tablePrivateDNSZoneName = 'privatelink.table.${environment().suffixes.storage}'

// AVM module for Blob Private Endpoint with private DNS zone
module blobPrivateEndpoint 'br/public:avm/res/network/private-endpoint:0.11.0' = if (enableBlob) {
  name: 'blob-private-endpoint-deployment'
  params: {
    name: 'blob-private-endpoint'
    location: location
    tags: tags
    subnetResourceId: '${vnet.id}/subnets/${subnetName}'
    privateLinkServiceConnections: [
      {
        name: 'blobPrivateLinkConnection'
        properties: {
          privateLinkServiceId: storageAccount.id
          groupIds: [
            'blob'
          ]
        }
      }
    ]
    customDnsConfigs: []
    // Creates private DNS zone and links
    privateDnsZoneGroup: {
      name: 'blobPrivateDnsZoneGroup'
      privateDnsZoneGroupConfigs: [
        {
          name: 'storageBlobARecord'
          privateDnsZoneResourceId: enableBlob ? privateDnsZoneBlobDeployment.outputs.resourceId : ''
        }
      ]
    }
  }
}

// AVM module for Queue Private Endpoint with private DNS zone
module queuePrivateEndpoint 'br/public:avm/res/network/private-endpoint:0.11.0' = if (enableQueue) {
  name: 'queue-private-endpoint-deployment'
  params: {
    name: 'queue-private-endpoint'
    location: location
    tags: tags
    subnetResourceId: '${vnet.id}/subnets/${subnetName}'
    privateLinkServiceConnections: [
      {
        name: 'queuePrivateLinkConnection'
        properties: {
          privateLinkServiceId: storageAccount.id
          groupIds: [
            'queue'
          ]
        }
      }
    ]
    customDnsConfigs: []
    // Creates private DNS zone and links
    privateDnsZoneGroup: {
      name: 'queuePrivateDnsZoneGroup'
      privateDnsZoneGroupConfigs: [
        {
          name: 'storageQueueARecord'
          privateDnsZoneResourceId: enableQueue ? privateDnsZoneQueueDeployment.outputs.resourceId : ''
        }
      ]
    }
  }
}

// AVM module for Table Private Endpoint with private DNS zone
module tablePrivateEndpoint 'br/public:avm/res/network/private-endpoint:0.11.0' = if (enableTable) {
  name: 'table-private-endpoint-deployment'
  params: {
    name: 'table-private-endpoint'
    location: location
    tags: tags
    subnetResourceId: '${vnet.id}/subnets/${subnetName}'
    privateLinkServiceConnections: [
      {
        name: 'tablePrivateLinkConnection'
        properties: {
          privateLinkServiceId: storageAccount.id
          groupIds: [
            'table'
          ]
        }
      }
    ]
    customDnsConfigs: []
    // Creates private DNS zone and links
    privateDnsZoneGroup: {
      name: 'tablePrivateDnsZoneGroup'
      privateDnsZoneGroupConfigs: [
        {
          name: 'storageTableARecord'
          privateDnsZoneResourceId: enableTable ? privateDnsZoneTableDeployment.outputs.resourceId : ''
        }
      ]
    }
  }
}

// AVM module for Blob Private DNS Zone
module privateDnsZoneBlobDeployment 'br/public:avm/res/network/private-dns-zone:0.7.1' = if (enableBlob) {
  name: 'blob-private-dns-zone-deployment'
  params: {
    name: blobPrivateDNSZoneName
    location: 'global'
    tags: tags
    virtualNetworkLinks: [
      {
        name: '${resourceName}-blob-link-${take(toLower(uniqueString(resourceName, virtualNetworkName)), 4)}'
        virtualNetworkResourceId: vnet.id
        registrationEnabled: false
        location: 'global'
        tags: tags
      }
    ]
  }
}

// AVM module for Queue Private DNS Zone
module privateDnsZoneQueueDeployment 'br/public:avm/res/network/private-dns-zone:0.7.1' = if (enableQueue) {
  name: 'queue-private-dns-zone-deployment'
  params: {
    name: queuePrivateDNSZoneName
    location: 'global'
    tags: tags
    virtualNetworkLinks: [
      {
        name: '${resourceName}-queue-link-${take(toLower(uniqueString(resourceName, virtualNetworkName)), 4)}'
        virtualNetworkResourceId: vnet.id
        registrationEnabled: false
        location: 'global'
        tags: tags
      }
    ]
  }
}

// AVM module for Table Private DNS Zone
module privateDnsZoneTableDeployment 'br/public:avm/res/network/private-dns-zone:0.7.1' = if (enableTable) {
  name: 'table-private-dns-zone-deployment'
  params: {
    name: tablePrivateDNSZoneName
    location: 'global'
    tags: tags
    virtualNetworkLinks: [
      {
        name: '${resourceName}-table-link-${take(toLower(uniqueString(resourceName, virtualNetworkName)), 4)}'
        virtualNetworkResourceId: vnet.id
        registrationEnabled: false
        location: 'global'
        tags: tags
      }
    ]
  }
}
