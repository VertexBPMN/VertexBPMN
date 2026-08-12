# Data Model – History Cleanup

HistoryCleanupPolicy (Id, TenantId, Entity, RetentionDays, IncludeVariables, LegalHold, CreatedAt, UpdatedAt)
HistoryCleanupExecution (Id, PolicyId, StartedAtUtc, FinishedAtUtc, Status, DeletedCount, Error)

Indizes
- policy: (tenant_id, entity)
- exec: (policy_id, started_at_utc)
