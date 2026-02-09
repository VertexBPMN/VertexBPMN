# Spec – History Cleanup

Muss
- Policies je Tenant/Key: (entity, retentionDays, includeVariables, legalHold)
- Scheduler: Zeitfenster-basiert, konfigurierbarer Durchsatz, Backoff
- Dry-Run + Report, Idempotent
- API: Create/Update Policy, Run Now, Get Executions
- Cleanup-Ziele: History Events, Completed Instances, Orphan Variables

Abnahme
- 10M History Events: Cleanup < 30 min bei moderater Last
- Kein Einfluss auf Runtime-Latenz > p95 50ms
