# Plan – Migration & Batch API
Date: 2025-09-17

1) Research: Mappingregeln (1:n, removed nodes, changed gateways)
2) Contracts: MigrationPlan, Validation, Execution, BatchOps
3) Runtime: Planner, Validator, Executor, Incident-Erzeugung
4) Tenancy: tenant-aware Migration/Batch
5) Observability: Fortschritt, Fehler, Retries
6) Tests: Small → Large, Interrupted Runs, Compensation/MI