# Spec – Migration & Batch

Muss
- MigrationPlan: mappings[], instructions (moveActivityId, updateVars…)
- Validate: dry-run, conflicts, coverage
- Execute: async Batch (concurrency, chunkSize), resume/retry
- BatchOps: pause/resume/cancel; Incident bei Fehlern
- History: Audit-Trail (who/when/what), diff-Snapshot
