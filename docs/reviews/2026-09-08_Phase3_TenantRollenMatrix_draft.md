# Phase 3 — Rollen-/Tenant-Matrix (Role & Tenant Authorization Matrix)

- **Date:** 2026-09-08
- **Scope:** ALL public REST controllers in `src/VertexBPMN.Api/Controllers/*.cs` (56 controllers, 3 DTO-only files excluded: `SimulationComparisonDto.cs`, `SimulationComparisonRequestDto.cs`, `VariableTraceDto.cs`).
- **Auth model (source of truth):** `src/VertexBPMN.Api/Security/SecurityConfiguration.cs`
  - FallbackPolicy = `RequireAuthenticatedUser()` (SecurityConfiguration.cs:47-50) → **every endpoint without an explicit `[Authorize]` requires only authentication; NO role check.**
  - Policies (SecurityConfiguration.cs:53-63): `AdminOnly`(Admin), `ProcessManager`(Admin,ProcessManager), `ReadOnly`(Admin,ProcessManager,ReadOnly), `ProcessViewer`(Admin,ProcessManager,ReadOnly), `ApiKeyRequired`.
  - Roles come from `ClaimTypes.Role`. Tenant scoping is manual via `User.IsInRole("Admin") ? null : User.FindFirstValue("tenant_id")`.
- Health check endpoints `/api/health*`,`/api/ready` are separately `MapHealthChecks(...).AllowAnonymous()` in `Program.cs:332-361`; `HealthController.cs` itself carries no `[Authorize]` (→ fallback = authenticated).

## Legend
- **AUTH-FB** = falls back to FallbackPolicy `RequireAuthenticatedUser()` → any authenticated user, **no role/tenant check enforced by the controller**.
- **SCOPED** = endpoint resolves tenant from the `tenant_id` claim (admin ⇒ null/global) and passes it into the service, or checks the loaded entity's `TenantId` against the caller before returning.
- **NEG-ID RISK** = endpoint accepts a resource identifier and performs the lookup WITHOUT tenant scoping ⇒ an authenticated user of tenant A can read/mutate tenant B's object by guessing the id.

---

## Summary Matrix (per controller)

| Controller file | Class/route auth | Endpoints | Tenant scoping pattern |
|---|---|---|---|
| AnalyticsController.cs `api/analytics` | `[Authorize]` FB | GET events; GET event-stats; GET trace/{processInstanceId}; POST predict-duration; GET events/by-tenant/{tenantId}; GET events/timeseries/{eventType}; GET metrics/process | **SCOPED** via `CurrentTenantId()` = claim (line 140-141); trace/timeseries/metrics filter `TenantId == CurrentTenantId()` (lines 53,99,115); GetEventsByTenant checks caller claim (line 85-86). Note: no Admin override (admin sees only own-tenant events — not a leak). |
| AuditController.cs `api/audit` | `[Authorize(Policy="ReadOnly")]` | GET logs | **SCOPED** — `db.AuditLogs.Where(log.TenantId == claim)` (line 24). |
| CaseDefinitionController.cs `api/case-definitions` | `[Authorize]` FB | POST deploy; GET {key}; POST {key}/start; GET instances/{id}/history; POST plan-items/{..}/complete; POST events/{..}; PUT case-file/{..}; POST discretionary-items/activate; GET instances/{id} | **SCOPED** — `Tenant()` = claim (line 116); every endpoint passes `tenant` into service. GetInstance: `GetInstanceAsync(caseInstanceId, tenant)` (line 54). |
| ConnectorController.cs `api/connectors` | `[Authorize(Policy="ReadOnly")]` | GET; GET {id}; POST; PUT {id}; PUT {id}/enabled; POST {id}/test; DELETE {id} | **SCOPED** — `ResolveTenant` (line 99-105): non-admin forced to claim tenant (forbidden if mismatched); ids looked up as `GetAsync(tenant, id)` (line 29). |
| ConnectorTemplateController.cs `api/connector-templates` | `[Authorize(Policy="ReadOnly")]` | GET; GET {id}; POST; PUT {id}; DELETE {id} | **SCOPED** — `Tenant()` (line 68-74) non-admin→claim; `GetAsync(tenant, id)` (line 27). |
| CredentialController.cs `api/credentials` | `[Authorize(Policy="ReadOnly")]` | GET; GET {id}; POST; PUT {id}; PUT {id}/secret; DELETE {id} | **SCOPED** — `ResolveTenant` (line 136-142) non-admin→claim; `GetAsync(tenant, id)` (line 36). |
| DebugController.cs `api/debug` | none → **FB** | POST trace | Stateless (body BPMN). No tenant data. |
| DecisionController.cs `api/decision` | `[Authorize]` FB | POST deploy; GET by-key; POST evaluate | **SCOPED** — `ResolveTenantId` = claim for non-admin (line 79-82), passed into service (lines 29,48,73). |
| DiagnosticsController.cs `api/diagnostics` | none → **FB** | POST bpmn; POST dmn | Stateless validation. No tenant data. |
| EngineController.cs `api/engine` | `[Authorize]` FB | GET capabilities | Static capabilities. No tenant data. |
| FeatureFlagController.cs `api/feature-flags` | `[Authorize(Policy="ReadOnly")]` | GET; PUT {flag} (AdminOnly) | Global feature flags, not tenant-scoped by design. |
| FormController.cs `api/forms` | `[Authorize(Policy="ReadOnly")]` | GET; GET {id}; POST; PUT {id}; DELETE {id} | **SCOPED** — `Tenant()` (line 18) non-admin→claim; `GetAsync(tenant, id)` (line 14). |
| HealthController.cs `api/health...` (controller) | none → **FB** (MapHealthChecks endpoints are AllowAnonymous in Program.cs) | 13 endpoints | Global health/ops; no tenant data. |
| HistoryController.cs `api/history` | `[Authorize]` FB | GET; GET by-process-instance/{id}; GET {id} | **SCOPED** — `ResolveTenantId`=claim (line 63-69); GetById checks `CanAccessTenant(evt.TenantId)` (line 59,71-72). |
| IdentityController.cs `api/identity` | none → **FB** | GET list-tenants; GET validate-user; GET password-management | **ListTenants returns ALL tenants** to any authenticated user (line 21); others are stub responses. |
| InspectorController.cs `api/inspector` | `[Authorize]` FB | GET process-instance/{id}/state | **SCOPED** — checks `instance.TenantId` vs claim (line 44-46) → Forbid. |
| LoadBalancerController.cs `api/load-balancer` | none → **FB** | GET status; POST workers; DELETE workers/{id}; POST heartbeat; GET workers; GET workers/{id}/health; POST rebalance; GET/PUT config | Global cluster infra, **no role & no tenant check** — any authenticated user can register/unregister workers & update LB config. |
| MLAnalyticsController.cs `api/ml` | `[Authorize]` FB | GET predict/completion/{processInstanceId}; POST predict/duration; GET predict/bottlenecks/{key}; GET optimize/{key}; POST train; GET export/training-data | **NOT claim-scoped** — passes caller-supplied `tenantId` query/body straight into service (lines 37,63,89,115,141,167); relies on service to throw `UnauthorizedAccessException`→Forbid. **NEG-ID risk at controller layer** (PredictCompletion). |
| ManagementController.cs `api/management` | none → **FB** | POST suspend|resume|delete-process-instance/{id}; GET metrics | **Role gap:** destructive suspend/resume/delete reachable by ANY authenticated user (no `[Authorize]`). Tenant-scoped for non-admin via `ResolveTenant`+`CanAccessInstanceAsync` (lines 49-61). |
| MetricsController.cs `api/metrics` | none → **FB** | GET; GET prometheus | Global engine metrics to any authenticated user. No tenant data. |
| MigrationController.cs `api/migration` | per-endpoint policies | 7 endpoints (plan/execute/status/rollback/validate/snapshot/restore) | **SCOPED** — `ResolveTenant` (line 231-238) non-admin→claim; tenant passed into service; feature-flag gated. |
| N8nImportController.cs `api/import/n8n` | `[Authorize(Policy="ProcessManager")]` | POST | **SCOPED** — `ResolveTenant` claim (line 25-32). |
| OAuth2Controller.cs `api/oauth2` | authorize AdminOnly; callback **AllowAnonymous** | POST authorize; GET callback | authorize SCOPED (line 48-54); callback is anon by design (uses state/code, line 41). |
| OpenApiImportController.cs `api/import/openapi` | `[Authorize(Policy="ProcessManager")]` | POST | **SCOPED** — `ResolveTenant` claim (line 32-39). |
| PerformanceController.cs `api/performance` | none → **FB** | 7 endpoints | Global system/process/worker metrics & monitoring control to any authenticated user; no tenant data. |
| PluginController.cs `api/plugins` | `[Authorize]` FB | load(Admin); unload(Admin); GET; GET {id}; enable(Admin); disable(Admin); execute(ProcMgr); GET extension-points; POST extension-points(Admin); GET {id}/service/{type} | Plugins are global infra, not tenant-scoped. **Role gap:** GET / GET {id} / GET extension-points / GET {id}/service/{type} are FB → any authenticated user can read plugin inventory/execute read endpoints. |
| PollingTriggerController.cs `api/polling-triggers` | `[Authorize]` FB | GET; GET {id}; POST; PUT {id}; DELETE {id}; POST {id}/poll-now | **SCOPED** — `ResolveTenantId`=claim (line 93-96); `GetAsync(id, tenant)` (line 29). |
| ProcessMigrationController.cs `api/process-migration` | per-endpoint ProcessManager | 3 endpoints | **SCOPED** — `ResolveTenant()`=claim (line 131-132) passed to service. |
| RepositoryController.cs `api/repository` | `[Authorize]` FB | GET; GET {id}; POST; DELETE {id} | **SCOPED** — GetById/Delete check `CanAccessTenant` (lines 45,102); list scoped by claim. |
| RuntimeController.cs `api/runtime` | `[Authorize]` FB | POST start; GET {id}; GET | **SCOPED** — GetById checks `CanAccessTenant` (line 57,79-80); list by claim. |
| SimulationController.cs `api/simulation` | none → **FB** | POST scenario/{scenarioId}; POST | **NEG-ID RISK** — `SimulateScenario` loads scenario by id with no tenant check (line 37-38); `Simulate` uses caller `TenantId` from body (line 67). |
| SimulationAnalyticsController.cs `api/simulation-analytics` | none → **FB** | POST compare; POST variable-trace/{n}; POST steps; POST summary | Stateless (client-supplied `SimulationResult` in body; hash-verified). No server tenant data. |
| SimulationScenarioController.cs `api/simulation-scenario` | none → **FB** | GET; GET {id}; POST; PUT {id}; DELETE {id} | **NEG-ID RISK** — no claim-scoping anywhere; GET uses caller `tenantId ?? ""` (line 24), GET/PUT/DELETE by raw id (lines 41,85,94), Create sets `TenantId` from body (line 66). |
| TaskController.cs `api/task` | `[Authorize]` FB | GET; GET {id}; POST {id}/claim(ProcMgr); POST {id}/complete(ProcMgr); POST {id}/delegate(ProcMgr) | **SCOPED** — `GetById` checks `CanAccessTenant` (line 40); mutations via `CanMutateTaskAsync` (lines 48,71,83,88-92). |
| TaskIoSnapshotController.cs | `[Authorize]` FB | GET api/process-instances/{pid}/tasks/{el}/io-snapshots | **NEG-TENANT RISK** — `ResolveTenantId` (line 54-63) accepts caller-supplied `tenantId` query verbatim (line 56-57) with NO admin/claim check; non-admin can read another tenant's snapshots. |
| TenantController.cs `api/tenant` | `[Authorize(Policy="ReadOnly")]` | GET; GET {id}; POST(Admin); PUT(Admin); DELETE(Admin) | **Enumeration gap** — GET returns all tenants (line 23); GET {id} by id (line 29) — any ReadOnly-rôle user sees the whole tenant catalog. |
| TestRunsController.cs `api/test-runs` | `[Authorize(Policy="ProcessManager")]` | POST | **SCOPED** — `ResolveTenantId`=claim (line 50-53). |
| VertexAuthorizationController.cs `api/vertex/authorization` | `[Authorize(Policy="ReadOnly")]` | GET; POST(AdminOnly); DELETE(AdminOnly) | GET SCOPED via `CurrentTenantId()` (line 28,83-85). POST/DELETE are AdminOnly and operate on request ids with tenant from referenced group — admin-only. |
| VertexDecisionDefinitionController.cs `api/vertex/decision-definition` | `[Authorize]` FB | GET; GET {key}/xml; PUT {key}/xml(501) | **SCOPED** — `ResolveTenantId`=claim (line 62-65); `GetDecisionByKeyAsync(key, tenant)` (line 37). |
| VertexDecisionInstanceController.cs `api/vertex/decision-instance` | `[Authorize]` FB | GET | **SCOPED** — `ResolveTenantId`=claim + `ListInstancesAsync(..., tenant)` (line 27). |
| VertexDeploymentController.cs `api/vertex/deployment` | `[Authorize]` FB | GET | **SCOPED** — `ResolveTenantId`=claim (line 38-41). |
| VertexGroupController.cs `api/vertex/group` | `[Authorize(Policy="ReadOnly")]` | GET; GET {id}; POST(Admin); POST {id}/users(Admin); DELETE {id}/users(Admin) | GET/GET{id} SCOPED via `CurrentTenantId()` (line 28,36,42-44). Write ops AdminOnly (create/add/remove). |
| VertexHistoricTaskInstanceController.cs `api/vertex/history/task` | `[Authorize]` FB | GET | **SCOPED** — `ResolveTenantId`=claim (line 33-36). |
| VertexIncidentController.cs `api/vertex/incident` | `[Authorize]` FB | GET; GET {id}; POST {id}/resolve(ProcMgr) | **SCOPED** — `GetById` checks `CanAccessTenant` (line 38); Resolve too (line 48). |
| VertexJobController.cs `api/vertex/job` | none → **FB** | GET; GET {id} | **NEG-ID RISK + global list** — no `[Authorize]` (any authenticated), `ListDueAsync` all jobs (line 22), `GetByIdAsync(id)` no tenant check (line 29). |
| VertexMessageController.cs `api/vertex/message` | `[Authorize(Policy="ProcessManager")]` | POST | **SCOPED** — `ResolveTenantId`=claim (line 41-44). |
| VertexProcessDefinitionController.cs `api/vertex/process-definition` | `[Authorize]` FB | GET; GET {id}; GET {id}/xml; PUT {id}/xml(501) | **SCOPED** — `GetById`/`GetXml` check `CanAccessTenant` (lines 38,50). |
| VertexProcessInstanceController.cs `api/vertex/process-instance` | `[Authorize]` FB | GET; GET {id} | **SCOPED** — `GetById` checks `CanAccessTenant` (line 38). |
| VertexSignalController.cs `api/vertex/signal` | `[Authorize(Policy="ProcessManager")]` | POST | **SCOPED** — `ResolveTenantId`=claim (line 33-36). |
| VertexTaskController.cs `api/vertex/task` | `[Authorize]` FB | GET; GET {id}; GET {id}/form-schema; PUT {id}/form-schema(ProcMgr,501) | **SCOPED** — GetById/form-schema check `CanAccessTenant` (lines 41,53,66). |
| VertexUserController.cs `api/vertex/user` | `[Authorize]` FB | GET; GET {id}; POST(Admin); PUT(Admin); DELETE(Admin) | GET/GET{id} scoped via claim `tenant_id` (lines 27,35). Write ops AdminOnly. |
| VertexVariableController.cs `api/vertex/variable` | `[Authorize]` FB | GET {processInstanceId} | **NEG-ID RISK** — `GetVariablesAsync(processInstanceId)` no tenant check (line 23). |
| VisualDebugController.cs `api/visual-debug` | `[Authorize]` FB | 12 endpoints (see below) | **PARTIAL** — `GetProcessVisualization` checks tenant (line 209); **`StartDebuggingSession(pid)` (line 40) and `GetExecutionTrace(pid)` (line 265) do NOT**; session-scoped ops (stop/get/breakpoint/step/continue/variables) keyed by sessionId with no ownership check. |
| VisualDebuggerController.cs `api/visual-debugger` | `[Authorize]` FB | GET instance/{id}/state; POST instance/{id}/step | **PARTIAL** — `StepInstance` checks tenant (line 100); **`GetInstanceState(id)` returns full instance state (instance+BPMN+tokens+variables) with NO tenant check (lines 35-88)** ⇒ **NEG-ID RISK / data exposure**. |
| WebhookIngressController.cs `api/webhooks` | **AllowAnonymous** | GET/POST/PUT/PATCH/DELETE {**path} | Anonymous by design — authenticated via `X-VertexBPMN-Trigger-Secret`/`X-VertexBPMN-Signature` header (line 17-23). |
| WorkflowTriggerController.cs `api/triggers` | `[Authorize]` FB | GET; GET {id}; POST; PUT {id}; DELETE {id}; POST {id}/invoke(**AllowAnonymous**) | CRUD **SCOPED** via claim (line 139-142). `Invoke` is `[AllowAnonymous]` by design — guarded by one-time secret header (line 111-119). |

---

## 🚨 GAP LIST — negative-ID / cross-tenant access risks
Endpoints that accept a resource identifier and do **NOT** scope the lookup by tenant (authenticated user of tenant A can access tenant B's object by guessing the id):

1. **SimulationScenarioController** — `api/simulation-scenario` (no `[Authorize]` ⇒ any authenticated user)
   - `GET /` — `GetAllAsync(tenantId ?? string.Empty)` (SimulationScenarioController.cs:24) — caller-supplied tenant, no claim check. *Negative-tenant list.*
   - `GET /{id}` — `GetByIdAsync(id)` (SimulationScenarioController.cs:41) — **no tenant filter**. *NEG-ID.*
   - `PUT /{id}` — `UpdateAsync(id, scenario)` (SimulationScenarioController.cs:85) — **no tenant check on target id**. *NEG-ID.*
   - `DELETE /{id}` — `DeleteAsync(id)` (SimulationScenarioController.cs:94) — **no tenant check**. *NEG-ID.*
   - `POST /` — `TenantId` taken from body (SimulationScenarioController.cs:66) — any user can create into any tenant.

2. **SimulationController** — `api/simulation` (no `[Authorize]` ⇒ any authenticated user)
   - `POST /scenario/{scenarioId}` — loads scenario by id via `GetByIdAsync(scenarioId)` with **no tenant check** (SimulationController.cs:37-38), then simulates. *NEG-ID.*
   - `POST /` — uses `request.TenantId` from body (SimulationController.cs:67), no claim check.

3. **VertexJobController** — `api/vertex/job` (no `[Authorize]` ⇒ any authenticated user)
   - `GET /` — `ListDueAsync(...)` returns **all jobs across all tenants** (VertexJobController.cs:22).
   - `GET /{id}` — `GetByIdAsync(id)` with **no tenant check** (VertexJobController.cs:29). *NEG-ID.*

4. **VertexVariableController** — `api/vertex/variable` (`[Authorize]` FB)
   - `GET /{processInstanceId}` — `GetVariablesAsync(processInstanceId)` with **no tenant check** (VertexVariableController.cs:23). *NEG-ID: any authenticated user reads variables of any tenant's process instance by id.*

5. **VisualDebuggerController** — `api/visual-debugger`
   - `GET /instance/{id}/state` — returns full instance state (instance + BPMN XML + tokens + variables) with **NO tenant check** (VisualDebuggerController.cs:35-88; only `StepInstance` checks at :100). *NEG-ID / data exposure.*

6. **VisualDebugController** — `api/visual-debug`
   - `POST /session/start/{processInstanceId}` — starts debug on a process instance with **no tenant check** (VisualDebugController.cs:40).
   - `GET /trace/{processInstanceId}` — `GetExecutionTraceAsync(processInstanceId)` with **no tenant check** (VisualDebugController.cs:265). *NEG-ID.* (Inconsistent with `GetProcessVisualization` which checks tenant at :209.)
   - Session ops (`GET /session/{id}`, `POST /breakpoint|step/*|continue/{sessionId}`, `GET /variables/{sessionId}`) keyed by sessionId — no tenant/ownership check.

7. **TaskIoSnapshotController** — `api/process-instances/{pid}/tasks/{el}/io-snapshots`
   - `GET` — `ResolveTenantId(tenantId)` returns caller-supplied `tenantId` query **verbatim with no Admin/claim check** (TaskIoSnapshotController.cs:54-63; lines 56-57), then filters rows by it (line 39). Any authenticated user can pass any tenantId. *NEG-TENANT.*

8. **MLAnalyticsController** — `api/ml` (`[Authorize]` FB, no claim-scoping)
   - `GET /predict/completion/{processInstanceId}` — passes caller `tenantId` query directly into `PredictProcessCompletionAsync(processInstanceId, tenantId)` (MLAnalyticsController.cs:37); controller does **not** enforce claim tenant (service may Forbid via `UnauthorizedAccessException`). *NEG-ID at controller layer.*
   - `POST /predict/duration` (:63), `GET /predict/bottlenecks/{key}` (:89), `GET /optimize/{key}` (:115), `POST /train` (:141), `GET /export/training-data` (:167) — all forward caller-supplied `tenantId` with no claim check.

### Secondary / lower-severity findings (authZ, not negative-ID)
- **ManagementController** (`api/management`): suspend/resume/delete-process-instance reachable by **any authenticated user** (no `[Authorize]`) — tenant-scoped but role-unrestricted (destructive). GetMetrics exposes global metrics.
- **LoadBalancerController** (`api/load-balancer`): **any authenticated user** can register/unregister workers, update cluster config (no `[Authorize]`, no role).
- **IdentityController.ListTenants** (`api/identity`): **any authenticated user** lists the whole tenant catalog.
- **MetricsController / PerformanceController**: global engine/system metrics to **any authenticated user** (no `[Authorize]`).
- **PluginController** read endpoints (`GET`, `GET {id}`, `GET extension-points`, `GET {id}/service/{type}`): exposed to any authenticated user (FB) though plugins are global infra.
- **TenantController**: `GET /` and `GET /{id}` expose the full tenant catalog to any `ReadOnly`-rôle user (no tenant/ownership filter); writes AdminOnly.

---

## Negative-tenant test coverage (different tenant ⇒ Forbid/NotFound)
### Controllers WITH explicit negative-tenant coverage
| Controller | Test |
|---|---|
| TaskController | `TaskAndAnalyticsSecurityTests.TaskGetById_ForDifferentTenant_ReturnsForbid` (:15), `TaskClaim_ForDifferentTenant_ReturnsForbid` (:32); `ApiIntegrationTests.Task_List_And_GetById_Returns_NotFound` |
| RepositoryController | `TaskAndAnalyticsSecurityTests.RepositoryDelete_ForDifferentTenant_ReturnsForbidWithoutMutation` (:105) |
| HistoryController | `TaskAndAnalyticsSecurityTests.HistoryList_ForNonAdmin_UsesClaimTenantInsteadOfRequestedTenant` (:84); `ApiIntegrationTests.History_GetById_Returns_NotFound` |
| AnalyticsController | `AnalyticsApiTests.GetEventsByTenant_ForDifferentTenant_ReturnsForbidden` (:91); `AnalyticsQueries_ReturnOnlyEventsFromAuthenticatedTenant` (:101) |
| ManagementController | `ApiIntegrationTests.Management_RejectsWrongTenantAndAcceptsMatchingTenant` (:127); `ManagementServiceTests.SuspendProcessInstanceAsync_RejectsCrossTenantAccess` |
| InspectorController | `InspectorApiTests.InstanceState_IsTenantScopedAndReturnsPersistedState` |
| VisualDebugController | `PersistentVisualDebugStepApiTests.ProcessVisualizationEnforcesTenantIsolation` |
| VisualDebuggerController | `PersistentVisualDebugStepApiTests.StepOperationEnforcesTenantIsolation` |
| ConnectorController | `PersistentConnectorServiceTests.Create_RejectsCrossTenantCredentialAndNonHttpEndpoint` |
| ConnectorTemplateController | `ConnectorTemplateApiTests.TemplateLifecycle_IsTenantScoped_AndProtectsMutations` |
| CredentialController | `CredentialApiTests.Credentials_AreTenantIsolated_AndMutationsRequireAdmin`; `PersistentCredentialServiceTests.ListAndResolve_AreTenantIsolated` |
| RuntimeController | `PersistentRuntimePhase2AcceptanceTests.P2_AC_05_Runtime_read_is_tenant_authorized` |
| VertexSignalController | `PersistentRuntimePhase2AcceptanceTests.P2_AC_02_Signal_broadcast_is_tenant_isolated` |

### Controllers with NO negative-tenant test (no "different tenant ⇒ Forbid/NotFound" coverage)
- **SimulationScenarioController** ❌ (has NEG-ID gaps, no test)
- **SimulationController** ❌ (has NEG-ID gap, no test)
- **SimulationAnalyticsController** ❌ (stateless, no test)
- **VertexJobController** ❌ (has NEG-ID gap, no test)
- **VertexVariableController** ❌ (has NEG-ID gap, no test)
- **VertexTaskController** ❌
- **TaskIoSnapshotController** ❌ (has NEG-TENANT gap, no test)
- **MLAnalyticsController** ❌ (has NEG-ID gap at controller layer, no test; only `HistoricalPredictiveAnalyticsServiceTests` at service level)
- **VertexProcessInstanceController / VertexProcessDefinitionController / VertexDeploymentController / VertexDecisionDefinitionController / VertexDecisionInstanceController / VertexHistoricTaskInstanceController** ❌
- **VertexIncidentController / VertexMessageController / VertexUserController / VertexAuthorizationController / VertexGroupController** ❌
- **CaseDefinitionController / DecisionController / FormController / RepositoryController(list/GetById) / HistoryController(GetById is NotFound-tested, claim-tested)** ❌ partial
- **PollingTriggerController / WorkflowTriggerController** ❌ (CRUD tenant scoping untested)
- **MigrationController / ProcessMigrationController** ❌
- **TenantController / IdentityController / FeatureFlagController / EngineController / HealthController / MetricsController / PerformanceController / LoadBalancerController / PluginController / AuditController / OAuth2Controller / DebugController / DiagnosticsController / OpenApiImportController / N8nImportController / TestRunsController / WebhookIngressController** ❌

---

## Bottom line
- The **core runtime/repository/task/history/incident/decision/connector/credential/trigger** controllers are consistently tenant-scoped (claim-based, admin⇒global) and the get-by-id paths enforce per-entity `TenantId` checks.
- **9 distinct negative-ID / cross-tenant access gaps** were found (SimulationScenario, Simulation, VertexJob, VertexVariable, VisualDebugger.GetInstanceState, VisualDebugController trace/start, TaskIoSnapshot, MLAnalytics) — these are the highest-priority fixes.
- A **second class of authZ gaps**: several controllers relying solely on the `FallbackPolicy` (any authenticated user, no role check) expose privileged/global operations — `ManagementController` (suspend/delete instances), `LoadBalancerController` (worker/cluster control), `MetricsController`/`PerformanceController`/`IdentityController.ListTenants` (global metrics/tenant catalog).
- **Negative-tenant test coverage is sparse**: only 13 controllers have explicit "different tenant ⇒ Forbid/NotFound" tests; all 9 gap controllers except none are covered, and most read-only/management controllers have no negative-tenant test at all.
