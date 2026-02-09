# Spec – Multi-Tenancy

Muss
- Jede API akzeptiert `X-Tenant-Id` (Header) oder Token-Claim
- Deployments/Definitions/Instances/Jobs/History enthalten tenantId
- Queries sind tenant-scoped (default deny)
- Audit/History tenant-scoped; Cleanup respektiert Tenant-Retention
- Fehler: 403 bei Cross-Tenant-Zugriffen, 400 bei fehlender TenantId (konfigurierbar)

Abnahme
- Suite von Negativtests (Cross-Tenant Zugriff) grün
- Lasttest: 3 Tenants parallel, Isolationsmetriken stabil
