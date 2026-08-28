# Produkt-Support- und Acceptance-Matrix

**Stand:** 28.08.2026
**Geltungsbereich:** Full Product Support; bewertet wird der öffentlich nutzbare, persistente End-to-End-Pfad. Parser- oder Unit-Tests allein begründen keinen Produktsupport. Jede in dieser Matrix aufgeführte Fähigkeit ist qualifiziert und unterstützt.

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
| Complex Gateway | `supported` | Bedingte Mehrfachaktivierung, Default-Flow sowie persistente Joins mit ausgewerteter `activationCondition` sind durch FPS-BPMN-04/04B belegt. |
| Eingebetteter Subprozess | `supported` | Leere, verschachtelte und wartende Scopes, parallele/sequenzielle Multi-Instance-Subprozesse, wiederverwendbare interrupting/non-interrupting Event-Subprozesse sowie Call Activities mit persistenter Parent-/Child-Instanz und Parent-Fortsetzung sind durch FPS-BPMN-05, 08–09 und 12–16 belegt. |
| Timer Catch sowie interrupting und non-interrupting Boundary Timer | `supported` | Fällige Jobs, Wait-Token, Lease und genau eine Fortsetzung sind end-to-end getestet. Der nicht-unterbrechende Pfad lässt den angehängten Task aktiv. ISO-8601-`timeDate`-, `timeDuration`- und `timeCycle`-Angaben werden sicher in persistente Fälligkeiten überführt. |
| Message-/Signal-Catch und Correlation | `supported` | Persistente Subscriptions werden tenantbezogen korreliert bzw. gesendet, konsumiert und genau einmal fortgesetzt. |
| Kompensation | `supported` | Erfolgreich abgeschlossene Aktivitäten registrieren persistente Compensation-Subscriptions; standardkonforme Associations binden Boundary Events an Handler. Implizite Throws bleiben im aktuellen Scope, wiederholte Registrierungen werden nicht zusammengelegt und Handler laufen persistent, sequenziell und in umgekehrter Abschlussreihenfolge. Eingebettete Compensation Event Subprocesses sowie Transaction-Cancel mit vollständig abgeschlossener Kompensation vor dem Cancel-Boundary-Pfad sind durch P2-AC-03 und FPS-COMPENSATION-01 bis -04 belegt. |
| Error, Escalation, Cancel und Terminate | `supported` | Hierarchische Error-/Escalation-Auflösung unterstützt Boundary Events und Event-Subprozesse einschließlich Root-Scope; interrupting Catches stornieren Queue, Tokens, Tasks, Jobs und Subscriptions, non-interrupting Escalations erhalten den ursprünglichen Pfad. Transaction Cancel und Scope-/Root-Terminate sind persistent über FPS-BPMN-06–09 und 17–18 belegt. |
| Neustart, mehrere API-Replikate und Idempotenz | `supported` | Gemeinsame relationale Datenbank, Inbox-Constraint, Optimistic Concurrency und Lease/Locking sind durch Host-Neustart- und Zwei-Replika-Verträge belegt. |
| Incident Recovery und Job Dead Letter | `supported` | Servicefehler erzeugen Incidents; Recovery setzt am betroffenen Knoten fort. Jobs besitzen Retry/Backoff, Lease und Dead-Letter-Zustand. |

## DMN

| Fähigkeit | Status | Nachweis und Grenze |
| --- | --- | --- |
| Einzelne Decision Table mit allen DMN-Hit-Policies | `supported` | `UNIQUE`, `FIRST`, `PRIORITY`, `ANY`, `COLLECT` samt `SUM`/`MIN`/`MAX`/`COUNT`, `RULE ORDER` und `OUTPUT ORDER` werden validiert und ausgewertet. PRIORITY/OUTPUT ORDER verwenden die deklarierten Output-Werte. |
| BPMN BusinessRuleTask-Integration | `supported` | Der persistente BPMN-Pfad löst direkte und Zeebe-kompatible Decision-Bindings tenantbezogen auf, wertet die persistierte DMN-Definition aus, persistiert Outputs/History/Outbox und routet nach dem Ergebnis. Fehler erzeugen einen Incident. |
| Vollständiges FEEL und DRD | `supported` | Eine gepinnte und reproduzierbar eingebettete FEEL-Laufzeit verarbeitet die vollständige Grammatik einschließlich Listen, Kontexte, Iterationen, Quantoren, temporaler Typen, Built-ins und kontextsensitiver Unary Tests; Syntaxfehler und Laufzeit-Warnings brechen fail-closed ab. Der gemeinsame API-/ProcessEngine-Pfad unterstützt mehrstufige, zyklusvalidierte Decision-Requirements-Graphen aus Decision Tables und Literal Expressions sowie Decision Services mit Output-, Encapsulated-Decision- und Input-Data-Referenzen. FPS-DMN-01 bis -05 belegen API- und lokalen Engine-Pfad. |

## CMMN

| Fähigkeit | Status | Nachweis und Grenze |
| --- | --- | --- |
| CMMN-Definition-API | `supported` | CMMN-1.1-Definitionen werden XML-sicher geparst, `definitionRef`-Verweise validiert, tenantbezogen persistent deployt und über REST sowie gRPC verwendet. |
| Case Lifecycle, Sentries, Discretionary Items und Wiederanlauf | `supported` | Case- und Plan-Item-Zustände, Case File und History sind relational persistent. Entry-/Exit-Sentries, OnParts/IfParts, verschachtelte Stages, Human-/Manual-/Service-Tasks, User Events und Discretionary Items laufen über REST, SDK, gRPC und MCP. Lifecycle- und Host-Neustart-Verträge belegen die Fortsetzung derselben Case-Instanz. |

## Plattform- und Betriebsfunktionen

| Fähigkeit | Status | Nachweis und Grenze |
| --- | --- | --- |
| REST API und .NET SDK für Deployment/Start | `supported` | REST startet qualifizierte BPMN-Modelle bis End-/Wait-State; die zentralen Verträge laufen über einen echten API-Host. |
| gRPC | `supported` | gRPC und MCP verwenden dieselbe persistente CMMN-Runtime wie REST, geben stabile Case-Instance-IDs zurück und sind mit Lifecycle-, Case-File-, Event-, Discretionary-Item- und History-Verträgen belegt. |
| EF-Core-Runtime-Persistenz | `supported` | Instanzen, Tokens, Variablen, Tasks, Jobs, Subscriptions, Incidents, Inbox, Outbox und Worker-Registrierungen sind relational modelliert und migriert. |
| Produktionskonfiguration und Mandantenschutz | `supported` | Production/Stage verlangen Connection Strings und persistenten Data-Protection-Keyring; Fake/InMemory/NoOp-Auflösungen und unsichere Script-/Connector-/Plug-in-Konfigurationen brechen den Start ab. |
| Externe Brokerzustellung der Outbox | `supported` | Der persistente Publisher least Nachrichten atomar und replika-sicher und liefert mit stabilen Message-IDs, Retry-/Dead-Letter-Semantik und Broker-Readiness an RabbitMQ oder Kafka. Echte RabbitMQ-/PostgreSQL-Akzeptanztests sind ein verpflichtendes separates CI-Gate. |
| Process Mining / Analytics | `supported` | Runtime-Ereignisse werden aus der transaktionalen Outbox mit stabiler `SourceEventId` wiederanlauffähig und idempotent in den persistenten Mining-Store projiziert. Tenantgeschützte Event-, Trace-, Zeitreihen- und Prozessmetrik-Endpunkte sowie der Retry ohne Duplikate sind durch FPS-ANALYTICS-01 und `AnalyticsApiTests` belegt; Management und Prometheus lesen persistente Betriebszähler. |
| Simulation und Simulation Analytics | `supported` | Der Engine-Simulator nutzt den produktiven sicheren BPMN-Parser und den Runtime-Bedingungsauswerter; Splits/Joins, eingebettete und Multi-Instance-Subprozesse mit iterationsisolierten Joins, explizit ausgewählte Event-Gateway- und interrupting/non-interrupting Event-Subprozess-Pfade sowie Call Activities mit bereitgestellten Definitionen erzeugen deterministische, MaxSteps-begrenzte Traces. Hash-gebundene Analytics lehnen manipulierte Traces ab; `DeterministicSimulationServiceTests` und P4-AC-05 belegen den öffentlichen Pfad. |
| Live-Prozessmigration | `supported` | Persistente, tenantgebundene Pläne referenzieren konkrete Source-/Target-Definition-IDs und können damit auch Versionen desselben Process-Keys migrieren. Vor jeder Mutation werden alle aktiven Tokens, Tasks, Jobs, Event-Subscriptions, Incidents und Multi-Instance-Zustände gegen das Zielmodell validiert; Snapshot, Runtime-Mapping, Definition-Wechsel, Audit-Outbox und optimistischer Instance-Count-Guard laufen in einer relationalen Transaktion. Dry-Run, dauerhafter Status, Rollback, Fortsetzung im Zielmodell, Cross-Tenant-Ablehnung und der Studio-Kompatibilitätspfad sind durch FPS-MIGRATION-01 bis -05 belegt. Bei deaktiviertem Feature bleiben beide APIs fail-closed. |
| bpmn.io Studio | `supported` | Gepinnte und reproduzierbar gebaute BPMN-/DMN-/CMMN-/Form-Assets laufen in echten Chromium-Verträgen. Import/Export, Vertex-Moddle-Erweiterungen, Properties, Low-Code-Mutationen, Quick Insert, lokale Token-Simulation, Viewer und sichtbare Fehlerpfade sind qualifiziert. FPS-STUDIO-01 führt ein Studio-Roundtrip-Artefakt über die produktiven Studio-Adapter gegen die reale persistente API vom Deployment über User-Task-Wait und Completion bis zum Prozessende aus. |
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
| `P4-AC-04` | Deaktivierte Migrations-APIs fail-closed mit HTTP 501 sperren | **grün** |
| `P4-AC-05` | Deterministische Simulation samt hash-gebundener Analytics ausführen und manipulierte Traces ablehnen | **grün** |
| `P4-AC-06` | Engine-Capabilities melden die qualifizierte persistente CMMN-Ausführung | **grün** |
| `P4-AC-07` | CMMN-gRPC und MCP führen Lifecycle, Events, Case File, Discretionary Items und History persistent aus | **grün** |
| `P4-AC-08` | CMMN-Host-Neustart erhält Case- und Discretionary-Item-Zustände und setzt dieselbe Instanz fort | **grün** |
| `FPS-DMN-01` | FEEL-Iteration, Kontext, Quantor und temporale Werte über Deployment und Evaluation der öffentlichen API | **grün** |
| `FPS-DMN-02` | Mehrstufiger DRD und Decision Service liefern ausschließlich die deklarierten Output Decisions | **grün** |
| `FPS-DMN-03` | ANY, COLLECT-Aggregation, RULE ORDER und OUTPUT ORDER führen ihre Standardsemantik aus | **grün** |
| `FPS-DMN-04` | Ungültige FEEL-Ausdrücke und Unary Tests werden beim Deployment abgewiesen | **grün** |
| `FPS-DMN-05` | Der lokale ProcessEngine-Pfad verwendet dieselbe mehrstufige DRD-/FEEL-Laufzeit wie die persistente API | **grün** |
| `FPS-STUDIO-01` | Produktive Studio-Adapter deployen ein Modeler-Roundtrip-Artefakt und führen es persistent bis Wait, Completion und End aus | **grün** |
| `FPS-MIGRATION-01` | Dry-Run, atomare Ausführung, dauerhafter Snapshot, Rollback und Fortsetzung im Zielmodell | **grün** |
| `FPS-MIGRATION-02` | Event-Gateway-Tokens, Subscriptions und Timer-Jobs migrieren und danach korrekt korrelieren | **grün** |
| `FPS-MIGRATION-03` | Konkrete Definition-IDs migrieren zwischen zwei Versionen desselben Process-Keys | **grün** |
| `FPS-MIGRATION-04` | Studio-Kompatibilitätsroute delegiert Preview und Execute an die qualifizierte transaktionale Engine | **grün** |
| `FPS-MIGRATION-05` | Source-/Target-Definitionen verschiedener Tenants werden abgewiesen | **grün** |
| `FPS-COMPENSATION-01` | Mehrere Compensation Handler laufen sequenziell in umgekehrter Abschlussreihenfolge | **grün** |
| `FPS-COMPENSATION-02` | Implizite Kompensation bleibt auf den aktuellen BPMN-Scope begrenzt | **grün** |
| `FPS-COMPENSATION-03` | Abgeschlossener Subprozess wird über seinen Compensation Event Subprocess kompensiert | **grün** |
| `FPS-COMPENSATION-04` | Transaction Cancel wartet auf vollständige Kompensation vor Aktivierung des Cancel Boundary Events | **grün** |

Das verpflichtende Full-Product-Support-Gate führt aktuell 47 konkrete Testfälle aus, darunter 19 persistente BPMN-Core-Verträge, Runtime-Outbox-Analytics, fünf Live-Migrations-, vier Compensation-, acht FEEL-/DRD- und einen Studio-Runtime-Vertrag. Sechs zusätzliche Phase-2-Verträge tragen `Category=Phase2Acceptance`. Supportaussagen gelten für die explizit aufgeführten und durch diese Gates qualifizierten Fähigkeiten.
