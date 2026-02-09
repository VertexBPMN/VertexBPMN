Du bist ein hochqualifizierter Coding-Agent (Senior Backend/Runtime-Engineer + Systems Architect + Test-Engineer). Deine Aufgabe ist es, **eine vollständige, produktionsreife BPMN 2.0 Prozess-Engine** zu entwerfen und zu implementieren in **C# 9** (target framework: net5.0), mit einem Schwerpunkt auf vollständiger BPMN-Konformität, DMN-Integration, Form-Rendering-Integration (bpmn-io toolkits) und erstklassiger Skalierbarkeit, Observability, Sicherheit und Tests.

Ziel: Das Projekt ist so robust, vollständig dokumentiert, getestet und performant, dass es in einer Programmier-/Architektur-Olympiade gewinnen würde.

**Wesentliche Anforderungen — kurz**
1. Vollständige und nachprüfbare Konformität mit **BPMN 2.0** (OMG Spec). Implementiere das Verhalten der Engine so weit wie möglich entsprechend der offiziellen Spezifikation. (BPMN-XML Parsing/Serialization inkl. ExtensionNamespaces). :contentReference[oaicite:1]{index=1}  
2. Funktionsumfang und APIs kompatibel/analog zu Camunda (RepositoryService, RuntimeService, TaskService, HistoryService, ManagementService, IdentityService, JobExecutor, REST API). Nutze Camunda als funktionales Referenz-Design. :contentReference[oaicite:2]{index=2}  
3. Editor/Rendering-Integration: volle Interoperabilität mit **bpmn-io** Toolkits (`bpmn-js`, `dmn-js`, `form-js`) — import/export von BPMN-XML/DMN XML und Unterstützung für form-JSON aus form-js. Stelle Integrations-Adapter bereit. :contentReference[oaicite:3]{index=3}  
4. DMN-Support: implementiere oder integriere eine DMN 1.3-konforme Entscheidungs-Engine (FEEL-Unterstützung soweit nötig). Plane Ausführung von Decision Tables und DRD-Integration. Nutze DMN-TCK/Spec zum Testen. :contentReference[oaicite:4]{index=4}  
5. Konformitätstests: Automatisches Durchlaufen der BPMN MIWG Test-Suite & DMN TCK (sofern möglich) als Gate für „Release Candidate“. :contentReference[oaicite:5]{index=5}

---

## Detaillierte Spezifikation (MUST / SHOULD / NICE-TO-HAVE)

### A. Funktionale Kern-Features (MUST)
- **BPMN 2.0 Prozessausführung**:
  - Start Events (message, timer, conditional, signal), End Events, Intermediate Events (throwing/catching), Boundary Events (interrupting/non-interrupting), Event Subprocess, Escalation/Error/Compensation Events.
  - Tasks: ServiceTask, ScriptTask, BusinessRuleTask, UserTask, ManualTask, SendTask, ReceiveTask.
  - Gateways: Exclusive (XOR), Inclusive (OR), Parallel (AND), Complex.
  - Subprocesses: Embedded, Transaction, Ad-hoc Subprocess.
  - Multi-instance (sequential/parallel), loopCharacteristics.
  - CallActivity (sync/async), message correlation.
  - DataObjects, DataStores, Variable scoping rules.
  - Transaction semantics for transactional subprocesses and compensation handling.
- **Job Executor & Asynchronität**:
  - Persistierte jobs (timers, async continuations, retries with backoff), pluggable job worker model (long-polling and push).
  - Exactly-once / at-least-once execution semantics with idempotence keys where appropriate.
- **Human-Task / Tasklist**:
  - Task assignment, candidate users/groups, claiming, completing, task form rendering (form-js JSON), task comments, attachments.
- **Repository & Deployment**:
  - Deploy BPMN/DMN/form artifacts as versioned deployments; support for process definition versioning and migration strategies.
- **Runtime Services (API parity with Camunda style)**:
  - RepositoryService (deployments, process definitions)
  - RuntimeService (start process by key/id, correlate messages, signal events)
  - TaskService (query/create/complete tasks)
  - HistoryService (detailed event history)
  - ManagementService (jobs, metrics)
  - IdentityService (lightweight pluggable)
- **History & Audit**:
  - Configurable history levels (none, activity, full) and a query API for event logs, variable history and case instance traces.
- **REST API**:
  - OpenAPI/Swagger spec, endpoints similar to Camunda REST for better tooling/compatibility (e.g., /engine-rest/process-definition, /process-instance, /task, /job).
- **Persistence**:
  - Database-agnostic with EF Core or Dapper support; provide SQL schema migrations (Flyway/EF Migrations). Must persist process definitions, activity instances, execution tokens, variables, jobs, incidents, historical events.
  - Provide a canonical SQL schema (tables with PKs/indices) and sample schema for PostgreSQL + SQL Server.
- **Transaction & Consistency**:
  - Use DB transactions for state changes; for distributed operations use reliable messaging or two-phase commit patterns where necessary.
- **Security**:
  - OAuth2 / OpenID Connect bearer tokens for REST; RBAC (scopes/roles) and tenant isolation.
- **Extensibility / Plugins**:
  - SPI hooks for custom connectors, expression functions, job handlers, and storage backends.

### B. Non-funktionale Anforderungen (MUST)
- **Testabdeckung**: Unit tests + Integration tests; Ziel: >=90% für Kernmodule. Include conformance tests (MIWG & DMN).
- **Observability**: OpenTelemetry tracing, metrics (Prometheus), structured logs (ELK), health endpoints.
- **Performance / Skalierbarkeit**:
  - Horizontally scalable job executor (partitioned queue), stateless API nodes + stateful DB cluster. Provide benchmarks and stress test harness.
- **Reliability**: Support for failover, leader election for singleton managers, idempotent job processing and retry semantics.
- **Documentation**: Auto-generated API docs (OpenAPI), developer guide, deployment guide, architecture diagrams, and runbooks.
- **Packaging & Delivery**:
  - Build via GitHub Actions, produce NuGet packages for core engine + worker SDK, Docker images for server components.
- **License**:
  - Choose an open source license compatible with bpmn-io/tooling and the team’s policy (document the choice and reasons).

### C. SHOULD / NICE-TO-HAVE
- Multi-tenant orchestration, BPMN simulation & dry-run, semantic model validation with helpful diagnostics, process migration tooling, BPMN visual debugger (step/inspect), Hot reload of process definitions.
- Optional: support for .NET Worker services and Kubernetes native operator for deployments.

---

## Architekturvorschlag (high level)
- **Control Plane (stateless)**: REST API/API Gateway, Authentication, Modeler Adapter, UI (Cockpit + Tasklist).  
- **Execution Plane (stateless)**: Multiple runtime nodes that execute tokens, persist state via DB. Use optimistic concurrency for token updates.  
- **Job Executor / Worker Pool**: Dedicated pool(s) for async jobs with partitioned queues (Kafka/RabbitMQ or DB-backed), long-polling or push worker SDK in C# (NuGet).  
- **Persistence Layer**: Relational DB (Postgres/MSSQL) for core state; optional NoSQL for metrics/analytics.  
- **Integrations**: Adapter layer for bpmn-io (upload/download BPMN XML), DMN engine component, form renderer service (serve JSON to UI).  
- **Observability**: Traces across REST→engine→job worker with OpenTelemetry context propagation.

---

## Datenmodell & Beispiel-SQL (essentiell)
Erstelle Tabellen (minimale/Beispiel-Spalten). Liefere vollständige DDL-Skripte für PostgreSQL und MS SQL.

Beispiel (Postgres-like pseudo DDL, implementiere wirklich ausführlich in Repo):

- `engine_deployment` (id UUID PK, name, deployed_at TIMESTAMP, tenant_id)
- `process_definition` (id UUID, key VARCHAR, version INT, deployment_id FK, bpmn_xml TEXT, resource_name)
- `process_instance` (id UUID, process_definition_id FK, business_key VARCHAR, state VARCHAR, start_time, end_time, root_scope_id)
- `execution_token` (id UUID, process_instance_id FK, activity_id, state, priority, created_at, updated_at)
- `variable` (id UUID, execution_id FK, name, type, value_json, created_at)
- `job` (id UUID, process_instance_id FK, handler_type, handler_configuration JSON, retries INT, due_date TIMESTAMP, lock_owner, lock_expiration)
- `task` (id UUID, execution_id FK, assignee, candidate_groups JSON, create_time, complete_time, form_key)
- `history_event` (id UUID, process_instance_id, activity_id, event_type, timestamp, payload JSON)
- `incident` (id UUID, job_id FK, incident_type, message, created_at)
- Indexe: FK, business_key, process_definition (key,version), next_due (job due_date + index), lock_owner.

(Ergänze noch Aktivitäten- und activity_instance Tabellen, user/group tables, tenant tables, and audit trails.)

---

## API / SDK Spezifikation (Beispiele)
- REST (OpenAPI): `/api/v1/process-definition/deploy`, `/api/v1/process-definition/key/{key}/start`, `/api/v1/process-instance/{id}`, `/api/v1/task/{id}`, `/api/v1/job/{id}/execute`
- C# SDK (NuGet) — wichtige Interfaces (signatures müssen geliefert werden):
  - `IRepositoryService { Task<Deployment> DeployAsync(Stream bpmnXml, string name, string tenant = null); }`
  - `IRuntimeService { Task<ProcessInstance> StartProcessByKeyAsync(string key, string businessKey = null, IDictionary<string, object> variables = null); Task CorrelateMessageAsync(string messageName, IDictionary<string, object> correlationKeys); }`
  - `ITaskService { Task<TaskDto[]> QueryTasksAsync(TaskQuery q); Task CompleteAsync(string taskId, IDictionary<string, object> variables = null); }`
  - `IJobWorker { Task PollAndExecuteAsync(CancellationToken ct); }`
- Worker SDK must support: long-poll, backoff, automatic retries, manual/automatic acknowledgement, metrics hooks and cancellation tokens.

---

## Engine-Runtime Semantik & Execution Rules
- Implement token semantics that conform to BPMN execution model: tokens are created/consumed on sequence flows; gateways evaluate expressions in the process context; parallel tokens behave independently; inclusive gateway evaluation must consider all outgoing flows satisfying conditions.
- Support for **asyncBefore/asyncAfter** markers; when a task is async the engine must persist state and create a job; job executor picks up and completes work.
- **Transaction boundaries**: persist state before dispatching external work; on job completion, resume token within DB transaction.

---

## Expression Language und Scripting
- **Default**: Provide a safe expression evaluator for conditional expressions and script tasks. Options:
  - Implement a sandboxed FEEL subset for DMN and a safe expression DSL for BPMN (recommended for portability & security).
  - Optionally support `C# scripting` using `Microsoft.CodeAnalysis.CSharp.Scripting` as a configurable plugin with strict sandbox restrictions and an allowlist.
- Expose a pluggable interface `IExpressionResolver` to register additional implementations (Jint for JS, FEEL engine, etc).

---

## DMN Integration
- Implement DMN evaluator supporting DMN 1.3 capabilities; at minimum: decision tables (unique, first, priority hit policies), FEEL expression evaluation (core subset), DRD imports.
- Integrate DMN execution as synchronous `BusinessRuleTask` and provide `IDecisionService` interface.
- Validate DMN against DMN TCK and include CI jobs to run DMN tests. :contentReference[oaicite:6]{index=6}

---

## bpmn-io Integration (Editor/Renderer)
- Provide import/export endpoints that accept BPMN XML produced by `bpmn-js`. Provide a mapping for engine extensions via vendor namespace `http://yourorg.com/schema/engine` and also allow `bpmn-js` properties panel plug-ins to present engine-specific attributes.
- Provide a small JS adapter example that plugs `bpmn-js` into the engine deployment flow (upload -> validate -> deploy).
- Support `form-js` JSON integration: persist form JSON with process definition and serve it in Task API for the UI.

References: bpmn-io toolkits / walkthrough. :contentReference[oaicite:7]{index=7}

---

## Tests, Conformance & CI
- **Unit Tests**: coverage for parser, token engine, gateway logic, job executor, variable handling.
- **Integration Tests**: end-to-end scenarios for message correlation, timers, compensation, multi-instance loops, callActivity across definitions, and a realistic business process sample (purchase order).
- **Conformance Tests**:
  - Integrate and run **BPMN MIWG Test Suite** automatically, report pass/fail with detailed diffs for model interchange and runtime behavior. :contentReference[oaicite:8]{index=8}
  - Run **DMN TCK** against the DMN engine and report compatibility.
  - Optional: run Zeebe’s `bpmn-tck` style tests where applicable. :contentReference[oaicite:9]{index=9}
- **Property + Fuzz Testing**: random BPMN models via generator and assert safety invariants (no deadlocks unless model dictates, variable isolation).
- **Performance Tests**: Baseline: measure throughput in process instance starts/sec, job processing/sec and 95th percentile latency for synchronous tasks. Provide a benchmarking harness (k6 or custom load tool).

---

## Deliverables (konkret)
1. **GitHub Repository** with:
   - `src/engine/*` full implementation
   - `src/sdk/*` C# worker SDK
   - `src/rest/*` REST API + OpenAPI spec
   - `src/ui-samples/*` sample adapter snippets for bpmn-io (JS)
   - `tests/unit`, `tests/integration`, `tests/conformance` (MIWG + DMN)
   - `docker` (Dockerfile + docker-compose for Postgres + engine)
   - `ci/` (GitHub Actions workflows: build, test, conformance, benchmark)
   - `docs/` (architecture.md, deployment.md, api.md, developer-guide.md)
2. **Architecture Diagrams** (SVG/PNG) and a component README.
3. **SQL DDL** (Postgres & MSSQL), migration scripts.
4. **OpenAPI** spec file.
5. **NuGet packages** for engine core + worker SDK + client.
6. **Sample Processes**: at least 8 representative BPMN models exercising complex features (timers, compensation, event subprocess, multi-instance).
7. **Conformance Report**: automated report showing MIWG test results and DMN TCK results (CSV + HTML), and an action item list for any unsupported test cases.

---

## Qualitätskriterien / Wie wird "Olympiasieger" entschieden?
- **Funktional**: Bestehende MIWG tests für modeling/import + runtime bestehen oder es gibt klare, minimal-impact Abweichungen mit rationale (ideally pass ≥95% of supported tests).
- **Robustheit**: Keine data-loss scenarios under failover testing, idempotent job processing, recovered job state after crash.
- **Performanz**: Demo-setup (4 cores, 16 GB RAM, Postgres on same host) should sustain at least N concurrent process instances with low latency — liefere Benchmarks/Graphs (N definieren wir im Benchmark-Plan).
- **Dokumentation & Developer Experience**: API + SDK sind gut dokumentiert, Beispiele leicht konsumierbar.
- **Observability & Debugging**: Distributed traces for process execution steps, job durations and incidents visible.

---

## Implementierungs-Meilensteine (Vorschlag)
1. **MVP (2–4 Wochen)**: Parser for BPMN XML, core token engine, variable store, basic service tasks, start/complete flows, simple REST API, basic persistence, CI with unit tests.
2. **Feature Complete Core (4–8 Wochen)**: Full BPMN features (events, gateways, multi-instance, call activities, job executor), user tasks with forms, history, basic DMN integration.
3. **Conformance & Hardening (3–6 Wochen)**: Run MIWG + DMN TCK, fix, add missing semantics, full integration tests, performance tuning.
4. **Polish & Delivery (2–4 Wochen)**: SDK, Docker images, docs, release.

(Die Wochen sind Richtwerte; passe die Zeitschätzung an das Team an.)

---

## Weitere präzise Hinweise an den Agenten
- **Behandle den BPMN-Parser mit großer Vorsicht**: vollständige Roundtrip-Tests (XML → model → XML) müssen identisch sein (oder differenzen minimal und dokumentiert). Nutze schema validation und preserve unknown vendor namespaces.
- **Konfigurierbarkeit**: history level, job executor thread pool, DB provider, tenant isolation toggles.
- **Sicherheit**: dokumentiere Risiken beim Aktivieren von ScriptTasks (Remote Code Execution). Default: ScriptTasks disabled — require explicit enablement and allow-list.
- **Code-Qualität**: StyleCop + Roslyn analyzers; öffentliche API mit XML docs; semantic versioning.
- **Fallbacks**: Wenn FEEL in .NET nicht vollständig implementierbar innerhalb des Zeitrahmens, implementiere eine speicherbare „FEEL-shim“ API, die Requests an eine separate FEEL Service (z. B. containerized Java FEEL engine) delegiert — aber dies muss transparent getestet werden.

---

## Akzeptanztests (beispielhaft)
- E2E: Deploy purchase_order.bpmn (with user tasks, service tasks, message events). Start instance, execute tasks, assert history events and final business data.
- Timer: Model with boundary timer event -> verify timer job created, due_date respected, timer triggers and moves flow as expected.
- Compensation: Execute transaction with compensation handlers; cause compensation and assert compensation handlers invoked correctly and history entries created.
- Message correlation: Two processes (sender/receiver) exchange messages — correlate by businessKey and assert receiver continues correctly.
- MIWG: Automatisch aus der test-suite: parse each test model, deploy, run scenario (as defined), assert engine behavior.

---

## Reporting / Artefakte nach Fertigstellung jeder Lieferung
- Release Notes (what’s supported / known gaps)
- Conformance test results (CSV/HTML)
- Perf Benchmark report (with graphs)
- OpenAPI + SDK Release (NuGet)
- Quickstart guide (5-minute tutorial to run sample process)

---

### Abschluss
Setze jede Aufgabe mit klaren PRs, atomic commits, und einem `CHANGELOG.md`. Leg alle Entscheidungen (Architektur, 3rd-party libs, tradeoffs) im Repo unter `docs/decisions/` ab.  

Wenn du loslegst, beginne mit:
1. Repo skeleton + CI
2. BPMN XML parser + model objects + roundtrip tests
3. Minimal runtime (single-threaded) that can start & complete a simple process
4. Dann Job Executor, persistence, and progressively enable more BPMN features.

Viel Erfolg — liefere zuerst das MVP Repository (kompilierbar + Tests ausgeführt) und danach iterieren wir auf Conformance und Performance.

