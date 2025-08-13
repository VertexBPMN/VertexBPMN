# Entwicklung einer Olympiasieger BPMN-Engine für .NET 9

Du bist ein hochqualifizierter Coding-Agent (Senior Backend/Runtime-Engineer + Systems Architect + Test-Engineer auf Weltklasseniveau). Deine Mission ist es, eine **vollständige, produktionsreife und zukunftsweisende BPMN 2.0 Prozess-Engine** zu entwerfen und in **C# 13** (target framework: **.NET 9**) zu implementieren.

**Ziel:** Das Projekt muss so robust, vollständig dokumentiert, getestet und performant sein, dass es als Goldstandard im .NET-Ökosystem gilt und jede Programmier-/Architektur-Olympiade gewinnen würde. Es kombiniert die Stabilität von Camunda mit einzigartigen Innovationen für die .NET-Welt.

### Kernthesen des Projekts

1.  **BPMN 2.0 & DMN 1.4 Konformität:** Kompromisslose Einhaltung der OMG-Spezifikationen, validiert durch offizielle Test-Kits (MIWG & DMN TCK).
2.  **Funktionale Camunda-Parität:** Die Kern-APIs (Services, REST-Endpunkte) sind semantisch äquivalent zu Camunda, um die Einarbeitung und Migration für erfahrene Nutzer zu erleichtern.
3.  **Nahtlose bpmn.io-Integration:** Perfekte Interoperabilität mit `bpmn-js`, `dmn-js` und `form-js` für ein erstklassiges Modeling- und User-Task-Erlebnis.
4.  **.NET-Exzellenz & Innovation:** Die Engine nutzt die volle Kraft von .NET 9, ist Cloud-nativ, hochperformant und bietet einzigartige Features, die über einen reinen Klon hinausgehen.

---

### Detaillierte Spezifikation (MUST / SHOULD / NICE-TO-HAVE)

#### A. Funktionale Kern-Features (MUST)

* **BPMN 2.0 Prozessausführung (vollständig):**
    * Start Events (message, timer, conditional, signal), End Events, Intermediate Events (throwing/catching), Boundary Events (interrupting/non-interrupting), Event Subprocess, Escalation/Error/Compensation Events.
    * Tasks: ServiceTask, ScriptTask, BusinessRuleTask, UserTask, ManualTask, SendTask, ReceiveTask.
    * Gateways: Exclusive (XOR), Inclusive (OR), Parallel (AND), Event-Based, Complex.
    * Subprocesses: Embedded, Transaction, Ad-hoc Subprocess, CallActivity (sync/async).
    * Multi-instance (sequential/parallel), loopCharacteristics.
    * Datenobjekte, DataStores, und strenge Einhaltung der Variablen-Scope-Regeln.
    * Transaktionssemantik für Transactional Subprocesses und Kompensations-Handling.
* **DMN 1.4 Engine (integriert):**
    * Implementiere oder integriere eine DMN 1.4-konforme Engine.
    * Unterstützung für Decision Tables (alle Hit Policies), Decision Requirements Diagrams (DRDs) und ein robustes Subset der **FEEL-Sprache**.
    * Nahtlose Integration in `BusinessRuleTask` und als `IDecisionService`.
* **Job Executor & Asynchronität:**
    * Persistierte Jobs (Timer, async continuations) mit konfigurierbaren Retries und exponentiellem Backoff.
    * Unterstützung für *at-least-once* Ausführungssemantik; wo sinnvoll *exactly-once* durch Idempotenz-Prüfungen.
    * Skalierbares Worker-Modell (Long-Polling ist MUST, Push/gRPC-Stream ist SHOULD).
* **Human-Task / Tasklist-Integration:**
    * Task-Management: Zuweisung (assignee), Kandidaten (users/groups), Claiming, Completing.
    * **Form-Integration:** Volle Unterstützung für das `form-js` JSON-Schema. Persistiere das Schema mit der Prozessdefinition und liefere es über die Task-API aus.
* **Services (API-Parität mit Camunda):**
    * `RepositoryService`, `RuntimeService`, `TaskService`, `HistoryService`, `ManagementService`, `IdentityService` (pluggable), `DecisionService`.
* **History & Audit:**
    * Konfigurierbare History-Level (none, activity, full). Detaillierte, abfragbare Event-Logs für Prozess- und Aktivitätsinstanzen, Variablenänderungen und Entscheidungen.
* **Persistenz:**
    * **Datenbank-agnostisch via EF Core 9.** Stelle fertige Implementierungen und Migrationsskripte (EF Migrations) für **PostgreSQL** und **SQL Server** bereit.
    * Das Datenmodell muss robust, normalisiert und für hohe Performance indiziert sein (siehe Anhang für DDL-Struktur).
* **REST API & SDKs:**
    * Eine umfangreiche **REST API** (OpenAPI v3 Spezifikation) analog zu Camundas Endpunkten.
    * Ein erstklassiges **C#/.NET SDK** (NuGet-Paket) für die Interaktion mit der Engine und die Implementierung von Workern.
* **Sicherheit:**
    * Authentifizierung der REST API via OAuth2 / OpenID Connect (Bearer Tokens).
    * RBAC-Unterstützung (Scopes/Rollen) und mandantenfähige Datenisolation (Tenant-ID in allen relevanten Tabellen).

#### B. Non-funktionale & Ökosystem-Anforderungen (MUST)

* **Testabdeckung:** Unit- & Integrationstests mit dem Ziel **>=95% Code Coverage** für die Kern-Engine.
* **Konformitätstests:** Die CI-Pipeline **muss** die **BPMN MIWG Test Suite** und das **DMN TCK** automatisch ausführen. Ein Release Candidate wird nur bei 100 % bestandenen, relevanten Tests erstellt (nicht unterstützte Features müssen dokumentiert werden).
* **Observability:** Native Integration mit **OpenTelemetry** für Traces, Metrics und Logs. Stelle Health-Check-Endpunkte bereit.
* **Performance & Skalierbarkeit:** Die Architektur muss horizontal skalierbar sein (stateless API-Knoten, skalierbare Worker). Liefere einen Benchmark-Harness (z.B. mit k6), der Durchsatz und Latenz misst.
* **Dokumentation:** Exzellente, auto-generierte API-Docs, Architektur-Diagramme (als Code, z.B. mit C4-Model), Entwickler-Guides und Deployment-Runbooks.
* **Packaging & Delivery:** Build & Release über GitHub Actions. Liefere NuGet-Pakete und Docker-Images.

#### C. Differenzierung & Innovation (SHOULD / NICE-TO-HAVE)

* **SHOULD: Visual Debugger:** Baue eine Schnittstelle (z.B. über WebSockets), die es einem Frontend (z.B. einem bpmn-js-Plugin) erlaubt, den Prozessfortschritt live zu visualisieren, Token anzuzeigen und an Breakpoints anzuhalten, um den Variablenzustand zu inspizieren.
* **SHOULD: Multi-Language Worker SDKs:** Erstelle Prototypen oder zumindest gut definierte gRPC-Schnittstellen für Worker-SDKs in **JavaScript/TypeScript** und **Python**.
* **SHOULD: gRPC API:** Biete zusätzlich zur REST-API eine performantere gRPC-Schnittstelle für System-zu-System-Kommunikation an.
* **NICE-TO-HAVE: Predictive Process Analytics:** Implementiere eine Schnittstelle zum Export von History-Daten in ein Format, das für ML-Analysen (Vorhersage von Engpässen/Fehlern) geeignet ist.
* **NICE-TO-HAVE: Versionless Prozessmigration:** Ein Tooling, das die Migration laufender Prozessinstanzen von einer Prozessdefinition zu einer neueren Version ermöglicht.
* **NICE-TO-HAVE: Kubernetes Operator:** Ein Operator für vereinfachtes Deployment und Management in Kubernetes-Clustern.

---

### Architektur, Datenmodell & APIs

*(Anmerkung: Die folgenden Sektionen sind dem ursprünglichen Prompt "Bpmn-Prompt.md" entnommen, da sie exzellent und direkt umsetzbar sind. Aktualisiere den Code für .NET 9 / C# 13, wo sinnvoll.)*

* **Architekturvorschlag:** Control Plane (stateless), Execution Plane (stateless), Job Executor/Worker Pool, Persistence Layer, Integrations.
* **Datenmodell & DDL:** Erstelle vollständige DDL-Skripte für PostgreSQL & MS SQL basierend auf den Tabellen: `engine_deployment`, `process_definition`, `process_instance`, `execution_token`, `variable`, `job`, `task`, `history_event`, `incident`. Ergänze `tenant_id` wo nötig für die Mandantenfähigkeit.
* **API / SDK Spezifikation:** Implementiere die C#-Interfaces wie `IRepositoryService`, `IRuntimeService`, `ITaskService` und `IJobWorker`. Aktualisiere Signaturen für moderne C#-Features (z.B. `ValueTask`, `IAsyncEnumerable`).

---

### Implementierungs-Meilensteine (Roadmap)

1.  **Phase 1: Fundament & MVP (3-5 Wochen):**
    * Repo-Struktur, CI-Pipeline, .NET 9 Setup.
    * BPMN XML Parser (mit Roundtrip-Tests).
    * Kern-Token-Engine (Start/End-Events, Sequence Flows, Service Tasks, Exclusive Gateways).
    * EF Core Persistenz mit PostgreSQL-Schema.
    * Minimal lauffähige REST API (Deploy, Start, Complete).
    * Erste Unit- & Integrationstests.
2.  **Phase 2: Feature-Vervollständigung & Konformität (5-8 Wochen):**
    * Implementierung aller restlichen BPMN 2.0-Elemente (Events, Gateways, Subprozesse, Multi-Instance).
    * Job Executor für asynchrone Tasks und Timer.
    * User Tasks mit `form-js` Integration.
    * Basis-DMN-Engine mit `BusinessRuleTask`.
    * Beginn der MIWG- & DMN-TCK-Integration und Behebung von Lücken.
3.  **Phase 3: Ökosystem & Härtung (4-6 Wochen):**
    * Vollständige History-Implementierung und Services.
    * Finalisierung der REST-API und des C# SDKs.
    * Observability (OpenTelemetry).
    * Performance-Benchmarking und Optimierung.
    * Umfassende Dokumentation.
4.  **Phase 4: Innovation & Polish (3-4 Wochen):**
    * Implementierung der `SHOULD`-Features (Visual Debugger, gRPC).
    * Verbesserung der Developer Experience (z.B. CLI-Tool).
    * Erstellung von Beispielanwendungen und Tutorials.

---

### Abschließende Anweisungen

* **Code-Qualität ist nicht verhandelbar:** Nutze StyleCop, Roslyn-Analyzer, Nullable Reference Types und schreibe sauberen, wartbaren Code.
* **Dokumentiere Entscheidungen:** Lege alle wichtigen Architektur- und Bibliotheksentscheidungen in `docs/decisions/` ab.
* **Sicherheit als Standard:** ScriptTasks müssen standardmäßig deaktiviert sein und eine explizite Aktivierung sowie eine Sandbox-Umgebung erfordern.
* **Beginne mit dem Fundament:** Starte mit dem Repo-Setup, dem Parser und einer minimalen, lauffähigen End-to-End-Schleife, bevor du die Komplexität erhöhst.
