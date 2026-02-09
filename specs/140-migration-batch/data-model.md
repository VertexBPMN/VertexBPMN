# Data Model – Migration/Batch

MigrationPlan (Id, SourceDefId, TargetDefId, Rules(json), TenantId, CreatedAt)
MigrationBatch (Id, PlanId, Status, Progress, StartedAtUtc, FinishedAtUtc, TenantId)
MigrationItem (Id, BatchId, InstanceId, Status, Attempts, LastError, UpdatedAtUtc)

Indizes
- plan: (tenant_id, source_def_id, target_def_id)
- batch: (plan_id, status)
- item: (batch_id, status)
