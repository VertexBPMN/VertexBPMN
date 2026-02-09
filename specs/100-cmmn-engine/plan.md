# Plan – CMMN 1.1 Engine
Date: 2025-09-17

Phases
1. Discover & Model (Research klären, CMMN-Semantik festziehen)
2. Contracts (OpenAPI-first: case-definition, case-instance, plan-item-instance, history)
3. Engine/Kernel (Lifecycle: Available → Enabled → Active → Completed/Terminated)
4. Persistence & Tenancy (EF Core Modelle, tenantId überall)
5. REST & Observability (Minimal APIs, OTel, Prometheus)
6. Tests (TDD: Contract → Integration → MIWG-ähnliche Case-Szenarien)
7. Hardening & Docs (Edge Cases, Performanceprofile, Runbooks)

Exit-Criteria Phase 1–2
- Alle [NEEDS CLARIFICATION] in research.md aufgelöst
- OpenAPI vollständig für Runtime+History, UTC-Zeiten, Paging/Filter
- Contract-Tests schlagen rot

Exit-Criteria Phase 3–6
- Alle Contract-/Integrationstests grün
- Conformance-Szenarien für Stages/Sentries/Repetition/Milestones grün
