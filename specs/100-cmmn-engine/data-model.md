# Data Model – CMMN

## Entities
- CaseDefinition (Id, Key, Version, Name, Xml, DeploymentId, TenantId, CreatedAt)
- CaseInstance (Id, DefinitionId, BusinessKey, State, Variables, TenantId, StartedAt, EndedAt)
- PlanItemInstance (Id, CaseInstanceId, DefinitionKey, Type, State, SentryState, RepetitionCounter, TenantId, CreatedAt, CompletedAt)
- Sentry (Id, CaseDefinitionId, SentryId, Type[Entry|Exit], TenantId)
- SentryPart (Id, SentryId, Kind[onPart|ifPart], EventRef, Condition, TenantId)
- CaseHistoryEvent (Id, CaseInstanceId, PlanItemInstanceId?, State, TimestampUtc, Payload, TenantId)

## Indizes (PostgreSQL)
- case_definition: (tenant_id, key, version)
- case_instance: (tenant_id, definition_id), (tenant_id, business_key), (state)
- plan_item_instance: (case_instance_id, state), (tenant_id, definition_key)
- case_history_event: (case_instance_id, timestamp_utc)

## Beziehungen
CaseDefinition 1-n CaseInstance  
CaseInstance 1-n PlanItemInstance  
CaseDefinition 1-n Sentry 1-n SentryPart
