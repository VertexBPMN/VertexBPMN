# Produkt-Support- und Acceptance-Matrix

**Stand:** 27.08.2026
**Geltungsbereich:** Abschluss Phase 5; bewertet wird der öffentlich nutzbare, persistente End-to-End-Pfad. Parser- oder Unit-Tests allein begründen keinen Produktsupport.

## Statusdefinitionen

| Status | Verbindliche Bedeutung |
| --- | --- |
| `supported` | Über die öffentliche API end-to-end implementiert, relational gespeichert und durch einen erfolgreichen Vertragstest belegt. |
| `partial` | Ein klar benanntes Subset funktioniert, weitergehende Standardsemantik oder ein Betriebsnachweis fehlt. |
| `unsupported` | Nicht implementiert oder ohne belastbaren Ausführungsnachweis; darf nicht als produktionsfähig beworben werden. |

## BPMN

| Fähigkeit / Elementgruppe | Status | Nachweis und Grenze |
| --- | --- | --- |
| Sicheres XML-Parsing, Validierung und Deployment | `supported` | Deployment verwirft ungültiges/gefährliches XML, ermittelt den Process-Key aus dem Modell und persistiert Definition und Deployment. |
| Tenantbezogene Definition-Versionierung | `supported` | Versionen werden pro `TenantScope` und Process-Key fortgeschrieben; ein eindeutiger DB-Constraint verhindert Doppelversionen. |
| None Start/End Event und Sequence Flow | `supported` | Der persistente API-Runtime-Pfad führt den Prozess bis zum End- oder Wait-State aus und schreibt History/Outbox. |
| Service Task | `supported` | Registrierte Handler werden im Runtime-Übergang ausgeführt; Fehler suspendieren die Instanz und erzeugen einen persistenten Incident. |
| User Task: Erzeugen, Claim, Complete, Resume | `supported` | Task und Wait-Token sind persistent; Completion setzt denselben Prozessfluss fort. |
| Parallel Gateway Split/Join | `supported` | Persistente Join-Ankünfte und Isolation mehrerer Instanzen sind durch die Phase-1-Verträge belegt. |
| Exclusive und Inclusive Gateway | `supported` | Der persistente Runtime-Pfad wertet bedingte Flows gegen Prozessvariablen aus, verwendet den deklarierten Default-Flow und erzeugt bei fehlender Route einen Incident. API-Verträge belegen Exclusive-, Inclusive- und Mehrfachtreffer-Semantik. |
| Event-based Gateway | `supported` | Message-, Signal- und Timer-Catch-Branches werden als konkurrierende persistente Wait-States angelegt; der Gewinner konsumiert bzw. storniert Tokens, Subscriptions und Jobs genau einmal. |
| Complex Gateway | `partial` | Bedingte Mehrfachaktivierung und Default-Flow sind persistent belegt. Vollständige BPMN-Activation-Condition- und Join-Semantik ist noch nicht freigegeben. |
| Eingebetteter Subprozess | `partial` | Leere und einfache eingebettete Subprozesse werden als Scope ausgeführt. Event-Subprozesse, Call Activities und echte Multi-Instance-Cardinality/Collection sind nicht freigegeben und werden nicht stillschweigend simuliert. |
| Timer Catch sowie interrupting und non-interrupting Boundary Timer | `supported` | Fällige Jobs, Wait-Token, Lease und genau eine Fortsetzung sind end-to-end getestet. Der nicht-unterbrechende Pfad lässt den angehängten Task aktiv. Timer Start Events und komplexe Cycles bleiben außerhalb des Subsets. |
| Message-/Signal-Catch und Correlation | `supported` | Persistente Subscriptions werden tenantbezogen korreliert bzw. gesendet, konsumiert und genau einmal fortgesetzt. |
| Kompensation | `partial` | Persistente Compensation-Subscriptions und ein getesteter Handler-Pfad sind vorhanden; vollständige BPMN-Scope-/Transaction-Semantik ist nicht behauptet. |
| Error, Escalation, Cancel und Terminate | `partial` | Interrupting Error Boundary, non-interrupting Escalation Boundary, Transaction Cancel Boundary und Root-Terminate sind persistent über die API belegt. Event-Subprozess-Catches sowie umfassende verschachtelte Scope-/Kompensationskombinationen sind noch nicht freigegeben. |
| Neustart, mehrere API-Replikate und Idempotenz | `supported` | Gemeinsame relationale Datenbank, Inbox-Constraint, Optimistic Concurrency und Lease/Locking sind durch Host-Neustart- und Zwei-Replika-Verträge belegt. |
| Incident Recovery und Job Dead Letter | `supported` | Servicefehler erzeugen Incidents; Recovery setzt am betroffenen Knoten fort. Jobs besitzen Retry/Backoff, Lease und Dead-Letter-Zustand. |

## DMN

| Fähigkeit | Status | Nachweis und Grenze |
| --- | --- | --- |
| Einzelne Decision Table mit allen DMN-Hit-Policies | `supported` | `UNIQUE`, `FIRST`, `PRIORITY`, `ANY`, `COLLECT` samt `SUM`/`MIN`/`MAX`/`COUNT`, `RULE ORDER` und `OUTPUT ORDER` werden validiert und ausgewertet. PRIORITY/OUTPUT ORDER verwenden die deklarierten Output-Werte. |
| BPMN BusinessRuleTask-Integration | `supported` | Der persistente BPMN-Pfad löst direkte und Zeebe-kompatible Decision-Bindings tenantbezogen auf, wertet die persistierte DMN-Definition aus, persistiert Outputs/History/Outbox und routet nach dem Ergebnis. Fehler erzeugen einen Incident. |
| Vollständiges FEEL und DRD | `unsupported` | Vergleiche, Bereiche, Alternativen, `not`, Literale und Datumswerte sind implementiert. Die vollständige offizielle DMN-TCK sowie Abhängigkeitsgraphen mit mehreren Decisions/Literal Expressions fehlen noch. |

## CMMN

| Fähigkeit | Status | Nachweis und Grenze |
| --- | --- | --- |
| CMMN-Definition-API | `supported` | CMMN-1.1-Definitionen werden XML-sicher geparst, `definitionRef`-Verweise validiert, tenantbezogen persistent deployt und über REST sowie gRPC verwendet. |
| Case Lifecycle, Sentries, Discretionary Items und Wiederanlauf | `supported` | Case- und Plan-Item-Zustände, Case File und History sind relational persistent. Entry-/Exit-Sentries, OnParts/IfParts, verschachtelte Stages, Human-/Manual-/Service-Tasks, User Events und Discretionary Items laufen über REST, SDK, gRPC und MCP. Lifecycle- und Host-Neustart-Verträge belegen die Fortsetzung derselben Case-Instanz. |

## Plattform- und Betriebsfunktionen

| Fähigkeit | Status | Nachweis und Grenze |
| --- | --- | --- |
| REST API und .NET SDK für Deployment/Start | `supported` | REST startet den persistenten BPMN-Subset bis End-/Wait-State; die zentralen Verträge laufen über einen echten API-Host. |
| gRPC | `supported` | gRPC und MCP verwenden dieselbe persistente CMMN-Runtime wie REST, geben stabile Case-Instance-IDs zurück und sind mit Lifecycle-, Case-File-, Event-, Discretionary-Item- und History-Verträgen belegt. |
| EF-Core-Runtime-Persistenz | `supported` | Instanzen, Tokens, Variablen, Tasks, Jobs, Subscriptions, Incidents, Inbox, Outbox und Worker-Registrierungen sind relational modelliert und migriert. |
| Produktionskonfiguration und Mandantenschutz | `supported` | Production/Stage verlangen Connection Strings und persistenten Data-Protection-Keyring; Fake/InMemory/NoOp-Auflösungen und unsichere Script-/Connector-/Plug-in-Konfigurationen brechen den Start ab. |
| Externe Brokerzustellung der Outbox | `supported` | Der persistente Publisher least Nachrichten atomar und replika-sicher und liefert mit stabilen Message-IDs, Retry-/Dead-Letter-Semantik und Broker-Readiness an RabbitMQ oder Kafka. Echte RabbitMQ-/PostgreSQL-Akzeptanztests sind ein verpflichtendes separates CI-Gate. |
| Process Mining / Analytics | `partial` | Ein persistenter Sink und dauerhafte Runtime-Outbox-Projektion sind registriert; Management- und Prometheus-Ausgaben lesen persistente Betriebszähler. Eine vollständige fachliche Analytics- und Process-Mining-Qualifizierung ist nicht nachgewiesen. |
| Simulation und Simulation Analytics | `unsupported` | Die vorhandenen Berechnungsdienste sind nicht fachlich qualifiziert. Öffentliche Ausführungs- und Analytics-Endpunkte antworten fail-closed mit HTTP 501. |
| Live-Prozessmigration | `unsupported` | Preview und Ausführung sind weder dauerhaft noch transaktional qualifiziert. Öffentliche Migrationsendpunkte antworten fail-closed mit HTTP 501. |
| bpmn.io Studio | `partial` | Gepinnte Assets und Moddle-Tests existieren. Der Engine-Test-Run verlangt nun einen echten End- oder persistenten Wait-State und Fehler werden sichtbar ausgegeben; vollständige Modell-/Runtime-Parität ist weiterhin nicht nachgewiesen. |
| Release- und Security-Qualifizierung | `supported` | Linux-/Windows-Build, API/Engine/Studio-Tests, OpenAPI-/Conformance-Gates, Dependency-Audits, CodeQL, Coverage, Secret-/Container-Scan, SPDX-SBOMs für API und Studio sowie echte RabbitMQ-/PostgreSQL-Verträge blockieren Releases. SDK und CLI werden zweimal byteidentisch gebaut, per SHA-256 geprüft und mit Provenance-Attestierung über NuGet OIDC veröffentlicht. |

## Verbindliche Acceptance-Fälle

Alle Verträge verwenden einen echten `WebApplicationFactory`-Host und relationale SQLite-Persistenz. Die fachlichen Assertions prüfen End-/Wait-State und dauerhafte Runtime-Datensätze.

| ID | Vertrag | Status |
| --- | --- | --- |
| `P1-AC-01` | Deploy → Start → Service Task → User Task → Complete → End | **grün** |
| `P1-AC-02` | Timer Catch/Boundary → Wait → Due → Resume | **grün** |
| `P1-AC-03` | Message und Signal → Wait → Correlate/Broadcast → Resume | **grün** |
| `P1-AC-04` | Host-Neustart während persistentem Wait-State | **grün** |
| `P1-AC-05` | Parallel Gateway und mehrere isolierte Instanzen | **grün** |
| `P2-AC-01` | Konkurrenzfähiger idempotenter Start | **grün** |
| `P2-AC-02` | Tenantisolierter Signal-Broadcast | **grün** |
| `P2-AC-03` | Persistente Kompensation über Wait/Complete | **grün** |
| `P2-AC-04` | Servicefehler → Incident → Recovery, genau einmal | **grün** |
| `P2-AC-05` | Negativer tenantbezogener Runtime-Lesezugriff | **grün** |
| `P2-AC-06` | Zwei API-Replikate teilen eine Idempotency-Claim | **grün** |
| `P4-AC-01` | Unterstützte DMN-Tabelle persistent deployen und über API auswerten | **grün** |
| `P4-AC-02` | PRIORITY-Hit-Policy verwendet deklarierte Output-Reihenfolge | **grün** |
| `P4-AC-02B` | Persistente DMN-Definition im BusinessRuleTask auswerten und Ergebnis für BPMN-Routing verwenden | **grün** |
| `P4-AC-03` | CMMN deployen/lesen, persistente Plan Items und Sentry-Fortsetzung bis Case-Ende | **grün** |
| `P4-AC-04` | Simulation und beide Migrations-APIs mit HTTP 501 sperren | **grün** |
| `P4-AC-05` | Simulation Analytics mit HTTP 501 sperren | **grün** |
| `P4-AC-06` | Engine-Capabilities melden die qualifizierte persistente CMMN-Ausführung | **grün** |
| `P4-AC-07` | CMMN-gRPC und MCP führen Lifecycle, Events, Case File, Discretionary Items und History persistent aus | **grün** |
| `P4-AC-08` | CMMN-Host-Neustart erhält Case- und Discretionary-Item-Zustände und setzt dieselbe Instanz fort | **grün** |

Die Phase-1-Suite führt aktuell 16 persistente BPMN-Verträge aus; sechs zusätzliche Phase-2-Verträge tragen `Category=Phase2Acceptance`. Das Phase-4-Gate führt zehn konkrete Testfälle aus. Nicht aufgeführte BPMN-, DMN- oder CMMN-Semantik ist nicht automatisch unterstützt.
