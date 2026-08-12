# VertexBPMN Studio API/CLI Parity Plan

## Ziel

Das Studio wird zur visuellen Control-Plane fuer VertexBPMN:

- CLI, Studio und API verwenden dieselben fachlichen Vertraege.
- Relevante API- und CLI-Funktionen sind im Studio auffindbar und ausfuehrbar.
- Nicht verfuegbare Funktionen werden anhand von Engine-Capabilities und Berechtigungen deaktiviert oder ausgeblendet.
- Simple- und Distributed-Engine werden korrekt unterstuetzt.
- Keine UI simuliert erfolgreiche Aenderungen ohne persistente API-Operation.

Der CLI-Befehl `dashboard` startet API und Studio bereits als lokalen Stack. Die fachliche Paritaet wird in den folgenden Phasen vervollstaendigt.

## Phasen

### 1. Feature-Matrix und Scope

Eine Matrix erfasst jeden API-Endpunkt und jedes CLI-Kommando mit API-Vertrag, Berechtigung, Engine-Abhaengigkeit, Studio-Seite, Studio-Service, Status und Test.

Bereiche: BPMN Runtime, CMMN/MCP, DMN, Tasks, Deployments, Prozessinstanzen, History/Audit, Monitoring, Analytics/ML, Debugging, Migration, Simulation, Tenants/Identitaet, Health/Performance, Jobs/Incidents/Messages/Signals/Variables, Plugins/Extensions, Feature Flags, Worker/Load-Balancer und Konfiguration.

**Abnahme:** Kein API-Endpunkt bleibt ohne dokumentierten Status.

### 2. Gemeinsame Studio-Service-Schicht

`IBpmnEngineService` wird in fachliche Clients aufgeteilt: Workflow, Tasks, Repository, History, Monitoring, Analytics, Decisions, Case Management, Debugging, Migration, Simulation, Administration, Identity, Health und Plugins.

Gemeinsame Client-Bausteine behandeln REST/gRPC-Aufrufe, ProblemDetails, Correlation-IDs, Cancellation, Timeouts, Retries, Tenant- und Benutzerkontext.

**Abnahme:** Razor-Seiten bauen keine eigenen API-URLs oder JSON-Vertraege.

### 3. Capability- und Berechtigungsmodell

Das Studio liest `/api/engine/capabilities`, Engine-Typ, Module, CMMN/DMN/Worker/Persistenz/gRPC-Unterstuetzung, Tenant und Benutzerrechte. Nicht verfuegbare Funktionen werden ausgeblendet oder eindeutig deaktiviert; der Server bleibt die Autoritaet fuer Berechtigungen.

**Abnahme:** Eine Studio-Version funktioniert korrekt mit Simple und Distributed.

### 4. Kern-Dashboard

BPMN-Deployments, Prozessdefinitionen, XML und Versionen, Prozessstart, Variablen, Prozessinstanzen, Suspend/Resume/Delete, Tasks, Formulare, Task-History, Prozess-History, Audit und SignalR-Monitoring werden vollstaendig API-gestuetzt.

**Abnahme:** Die zentralen Runtime- und Task-Ablauf werden ohne CLI bedienbar.

### 5. CMMN, gRPC und MCP

Ein echter gRPC-Client mit Authentifizierung und TLS wird fuer die vorhandenen Proto-Vertraege integriert. Das Studio bietet Case-Registrierung, Case-Ausfuehrung, Trace, User Events, Case-File-Updates, Ad-hoc-Subprozesse und historischen Kontext.

**Abnahme:** Ein CMMN-Case kann im Studio registriert, ausgefuehrt, veraendert und nachvollzogen werden.

### 6. DMN, Analytics und Simulation

DMN-Deployment/Evaluation, Prozessmetriken, Prognosen, Bottlenecks, Event-Statistiken, Simulationen, Variablen-Trace und Szenariovergleiche werden integriert.

### 7. Administration und Betrieb

Health, Readiness, Datenbank, Speicher, externe Abhaengigkeiten, Performance, Circuit Breakers, Rate Limits, Feature Flags, Tenants, Benutzer, Gruppen, Rollen, Worker, Load Balancer, Jobs, Incidents, Plugins, Extensions, Engine-Konfiguration und Connections werden angebunden.

Mutierende Admin-Funktionen benoetigen Berechtigungspruefung, Bestaetigung, Audit und Schutz gegen Doppelaufrufe.

### 8. Debugging und Migration

Visual Debugging mit Sessions, Breakpoints, Step-Funktionen, Variablen und Traces sowie Migration mit Plan, Validierung, Preview, Ausfuehrung, Status, Snapshot, Restore und Rollback werden integriert.

### 9. Tests und Betriebsnachweis

Service-Contract-Tests, API-Integrationstests fuer REST/gRPC, Blazor-UI-Tests fuer Kernworkflows und CLI-Dashboard-Tests sind ergaenzt.

## Initiale Feature-Matrix

| Bereich | API/CLI-Vertrag | Studio-Status | Prioritaet |
|---|---|---|---|
| BPMN Runtime | Repository, Runtime, Task, Management | Prozessinstanz- und Task-Ansichten mit zentralem Tenant-Filter; Start, Detailzugriff, Task-Aktionen und BPMN-Deployment-Aufrufe führen den effektiven Tenant-Kontext mit | P0 |
| Deployments und Definitionen | Repository, Vertex Deployment/Process Definition | API-gestützte Ansichten mit zentralem Tenant-Filter; BPMN-Upload, Versionen, XML-Ansicht und tenant-/rollen-geschütztes Löschen vorhanden | P0 |
| Tasks und Formulare | Task, Vertex Task | Task-Auflistung und Aktionen vorhanden; Tenant-Filter in API und Studio integriert | P0 |
| Prozess- und Task-History | History, Historic Task | Globale und prozessbezogene API-Abfrage mit erzwungener Tenant-Isolation; Studio-Historyansicht zeigt persistierte Ereignisse und reagiert auf Tenant-Wechsel | P0 |
| Monitoring und Notifications | SignalR Monitoring/Debug Hubs | Authentifizierte SignalR-Hubs und Studio-Monitoring vorhanden | P0 |
| Engine Capabilities | Engine Capabilities | Authentifizierter Client vorhanden; Simple/Distributed und Persistenz-/Worker-Fähigkeiten werden ausgewiesen | P0 |
| CMMN/MCP | REST CMMN und beide gRPC-Services | gRPC-Client und CMMN-Modeler integrieren Registrierung, Ausführung, User Events, Case-File, Ad-hoc und persistierten History-Abruf | P1 |
| DMN | Decision, Vertex Decision Definition/Instance | Deploy, Laden, Evaluation sowie read-only Definitions- und Instanzübersicht mit erzwungenem effektivem Tenant; nicht persistente XML-Updates bleiben explizit 501 | P1 |
| Analytics und ML | Analytics, ML Analytics, Metrics | Analytics-Client vorhanden; ML-Prognosen bleiben deaktiviert, bis eine echte historische Daten- und Modellpipeline angeschlossen ist | P1 |
| Simulation | Simulation, Simulation Analytics, Scenarios | Direkte Simulation, Summary/Variablen-Trace, Szenario-Laden/Speichern/Update/Delete, Vergleich und read-only Migration-Preview vorhanden | P1 |
| Administration | Tenants, Identity, Authorization, Feature Flags | Persistenter Tenant-CRUD mit Policies, tenant-scoped Benutzer-/Gruppen-/Membership-/Authorization-Lesezugriffe, Admin-Mutationen für Benutzer, Gruppen, Memberships und Autorisierungen, persistente Feature-Flags mit Admin-only-Schreiben und externe Studio-OIDC-Authentifizierung vorhanden; Passwortprüfung bleibt wegen externer Credential-Verwaltung explizit 501 | P1 |
| Health und Betrieb | Health, Performance, Metrics, Load Balancer | Health, Comprehensive Health, System-Metrics, Circuit Breaker sowie Performance-Dashboard/Load-Balancer-Status vorhanden | P1 |
| Debugging | Debug, Visual Debug, Visual Debugger | BPMN-Trace sowie Visual-Debug-Session, Breakpoint, Continue, Variableninspektion und persistentes Step-over über gespeicherte ExecutionTokens/HistoryEvents integriert; die dedizierte Prozessvisualisierung liest BPMN-XML, ExecutionTokens und HistoryEvents aus dem persistenten Zustand | P2 |
| Migration | Migration und Process Migration | Preview, autorisierte Ausführung, Status, Snapshot, Restore und Rollback integriert; mutierende Aktionen mit expliziter Bestätigung und persistenter zentraler HTTP-Auditierung | P2 |
| Erweiterungen | Plugins, Extensions, Connectors | Read-only Plugin-/Extension-Point-Ansicht integriert; mutierende Plugin-Verwaltung und Connector-API offen | P2 |
| Execution Details | Jobs, Incidents, Messages, Signals, Variables | Read-only Jobs, Incidents und Prozessvariablen integriert; Message Correlation und Signal Broadcast mit `ProcessManager`-geschützter API und Studio-UI integriert | P2 |

Die Matrix wird nach jeder implementierten Funktion aktualisiert. `Server-only`-Funktionen ohne sinnvolle Dashboard-Bedienung werden explizit als solche dokumentiert und nicht stillschweigend als Studio-Paritaet gezaehlt.

## Aktueller Implementierungsstand

Umgesetzt: Capability-Service mit CMMN-Navigation, BPMN-Kernworkflows, globale und prozessbezogene History-API mit erzwungener Tenant-Isolation, persistierte Studio-Historyansicht mit Tenant-Wechsel und SignalR-Monitoring, DMN-Deploy/Laden/Evaluation mit effektivem Tenant und geschütztem ProcessManager-Deploy, BPMN-Repository-, Definitions-, Deployment-Upload-, Task- und Prozessinstanzansichten mit zentralem Tenant-Filter, CMMN-gRPC-Client fuer Registrierung, Ausfuehrung, User Events, Case-File, Ad-hoc und persistierten History-Abruf, EF-persistierte CMMN-History-Snapshots, Entfernung simulierter Schreiboperationen und Demo-Komponenten, Health- und Betriebsansicht, Performance-Dashboard mit echten System-/Engine-Messwerten und explizit nicht konfigurierten Prüfungen, Analytics-Grundansicht, direkte BPMN-Simulation mit Szenario-Laden/Speichern/Update/Delete und Vergleichen, BPMN-Debug-Trace mit Visual-Debug-Session-Steuerung, Continue, Variableninspektion, persistentem Step-over und persistenter Prozessvisualisierung, persistente Feature-Flags mit Admin-only-Schreiben, persistente Tenant- und Benutzer-Leseansichten, read-only Execution Details fuer Jobs/Incidents/Variablen, Message-Correlation- und Signal-Broadcast-UI mit `ProcessManager`-Autorisierung sowie read-only Plugin-/Extension-Point-Ansicht, vollständiger Migration-Preview-/Ausführungs-/Status-/Snapshot-/Restore-/Rollback-Workflow mit `ProcessManager`-Autorisierung.

Offen: echte ML-Daten-/Modellpipeline und Export, Passwort-/Credential-Verwaltung sowie mutierende Plugin-/Connector-APIs. Die Tenant-Auswahl wird in allen tenant-fähigen Studio-Workflows einschließlich Prozessinstanz-Mutationen, BPMN-Modeler, Dashboard und Global Search übertragen und serverseitig geprüft. Die Studio-Fachbereiche Repository/Definitionen, Workflow/Prozessinstanzen, Tasks, Engine-Administration und Events sind über separate Verträge aufgeteilt; der bestehende HTTP-Client bleibt die konkrete Transportimplementierung. Der CLI-`--help`-Pfad ist als echter Host-Smoke-Test verifiziert. UI-Contract-Tests fuer Dashboard, Prozessdefinitionen und Tasks sind mit einem isolierten Playwright-Browser-Test-Host in `tests/VertexBPMN.Studio.UiTests` abgedeckt; der Host nutzt eine Stub-API und einen strikt auf `UiTest` begrenzten Authentifizierungsmodus. Der Lauf erfolgt mit `dotnet test tests/VertexBPMN.Studio.UiTests/VertexBPMN.Studio.UiTests.csproj`. Die beiden gRPC-Proto-Verträge sind durch aktive End-to-End-Contract-Tests abgedeckt; die Tests verifizieren alle deklarierten primären und MCP-RPCs gegen die echten Services.

## Definition of Done

- Jeder API-Endpunkt hat einen dokumentierten Studio-Status.
- Jeder unterstuetzte Endpunkt besitzt eine UI oder eine bewusst generische Verwaltungsansicht.
- Simple und Distributed werden korrekt unterschieden.
- CLI-Kernkommandos sind im Studio nachvollziehbar verfuegbar.
- gRPC/CMMN ist integriert und getestet.
- Keine simulierten Schreiboperationen existieren.
- Mutierende Operationen sind autorisiert und auditierbar.
- Kernworkflows sind automatisiert getestet.
- `dashboard` startet den vollstaendigen Stack verlaesslich.
