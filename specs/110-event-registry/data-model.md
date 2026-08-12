# Data Model – Event Registry

ChannelDefinition (Id, Key, Type[kafka|rabbit|jms|http], Config(json), TenantId)
EventDefinition (Id, Key, Schema(json), Mapping(json), TenantId)
Subscription (Id, ChannelId, EventKey, Correlation(json), Active, TenantId)

Index
- channel_definition: (tenant_id, key)
- event_definition: (tenant_id, key)
- subscription: (channel_id, event_key), (tenant_id, active)
