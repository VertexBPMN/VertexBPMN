@description('Location of the Service Bus namespace (defaults to resource group location).')
param location string = resourceGroup().location

@description('Name of the Service Bus namespace.')
param namespaceName string

@description('Environment name used for tagging, e.g. dev, test, prod.')
param environment string

resource namespace 'Microsoft.ServiceBus/namespaces@2022-10-01-preview' = {
  name: namespaceName
  location: location
  sku: {
    name: 'Premium'
    tier: 'Premium'
    capacity: 1
  }
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    minimumTlsVersion: '1.2'
  }
  tags: {
    Environment: environment
    Component: 'vertexbpmn-runtime'
  }
}

resource topic 'Microsoft.ServiceBus/namespaces/topics@2022-10-01-preview' = {
  parent: namespace
  name: 'vertexbpmn-runtime'
  properties: {
    defaultMessageTimeToLive: 'P14D'
    maxSizeInMegabytes: 1024
    requiresDuplicateDetection: false
    duplicateDetectionHistoryTimeWindow: 'PT10M'
    enableBatchedOperations: true
    supportOrdering: true
    autoDeleteOnIdle: 'P10675199DT2H48M5.4775807S'
  }
}

resource apiSubscription 'Microsoft.ServiceBus/namespaces/topics/subscriptions@2022-10-01-preview' = {
  parent: topic
  name: 'api'
  properties: {
    deadLetteringOnFilterEvaluationExceptions: false
    deadLetteringOnMessageExpiration: true
    maxDeliveryCount: 10
    defaultMessageTimeToLive: 'P14D'
    lockDuration: 'PT1M'
    autoDeleteOnIdle: 'P10675199DT2H48M5.4775807S'
  }
}

resource agentWorkerSubscription 'Microsoft.ServiceBus/namespaces/topics/subscriptions@2022-10-01-preview' = {
  parent: topic
  name: 'agent-worker'
  properties: {
    deadLetteringOnFilterEvaluationExceptions: false
    deadLetteringOnMessageExpiration: true
    maxDeliveryCount: 10
    defaultMessageTimeToLive: 'P14D'
    lockDuration: 'PT1M'
    autoDeleteOnIdle: 'P10675199DT2H48M5.4775807S'
  }
}

output namespaceId string = namespace.id

output fullyQualifiedNamespace string = namespace.properties.serviceBusEndpoint

output topicName string = 'vertexbpmn-runtime'

output apiSubscriptionName string = 'api'
