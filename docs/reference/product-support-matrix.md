# Produkt-Support- und Acceptance-Matrix

**Stand:** 25.08.2026
**Geltungsbereich:** Phase-2-Branch; bewertet wird der öffentlich nutzbare, persistente End-to-End-Pfad. Parser- oder Unit-Tests allein begründen keinen Produktsupport.

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
| Exclusive und Inclusive Gateway | `partial` | Knoten werden durchlaufen; vollständige Condition-/Default-Flow-Auswertung im persistenten Runtime-Pfad ist nicht freigegeben. |
| Event-based und Complex Gateway | `unsupported` | Parser-/Legacy-Engine-Code ist vorhanden, aber keine freigegebene persistente Semantik. |
| Eingebetteter Subprozess | `partial` | Leere und einfache eingebettete Subprozesse werden als Scope ausgeführt. Event-Subprozesse, Call Activities und echte Multi-Instance-Cardinality/Collection sind nicht freigegeben und werden nicht stillschweigend simuliert. |
| Timer Catch und interrupting Boundary Timer | `supported` | Fällige Jobs, Wait-Token, Lease und genau eine Fortsetzung sind end-to-end getestet. Timer Start Events, nicht-interrupting Boundary Timer und komplexe Cycles bleiben außerhalb des Subsets. |
| Message-/Signal-Catch und Correlation | `supported` | Persistente Subscriptions werden tenantbezogen korreliert bzw. gesendet, konsumiert und genau einmal fortgesetzt. |
| Kompensation | `partial` | Persistente Compensation-Subscriptions und ein getesteter Handler-Pfad sind vorhanden; vollständige BPMN-Scope-/Transaction-Semantik ist nicht behauptet. |
| Error, Escalation, Cancel und Terminate | `unsupported` | Keine freigegebene persistente Boundary-/Scope-Semantik. |
| Neustart, mehrere API-Replikate und Idempotenz | `supported` | Gemeinsame relationale Datenbank, Inbox-Constraint, Optimistic Concurrency und Lease/Locking sind durch Host-Neustart- und Zwei-Replika-Verträge belegt. |
| Incident Recovery und Job Dead Letter | `supported` | Servicefehler erzeugen Incidents; Recovery setzt am betroffenen Knoten fort. Jobs besitzen Retry/Backoff, Lease und Dead-Letter-Zustand. |

## DMN

| Fähigkeit | Status | Nachweis und Grenze |
| --- | --- | --- |
| Decision-Table-Parsing und ausgewählte Hit Policies | `partial` | Parser-, Unit- und API-Tests existieren; keine offizielle DMN-TCK als Release-Gate. |
| FEEL, DRD und BusinessRuleTask-Integration | `partial` | Subsets und Legacy-Pfade existieren; keine vollständige persistente BPMN/DMN-Ausführung oder FEEL-Konformität. |

## CMMN

| Fähigkeit | Status | Nachweis und Grenze |
| --- | --- | --- |
| CMMN-Parsing und Definition-API | `partial` | Einfache Definitionen werden migriert, deployt und gelesen. |
| Case Lifecycle, Sentries, Discretionary Items und Wiederanlauf | `unsupported` | Kein vollständiger persistenter Case-Lifecycle und keine verbindliche Conformance-Suite. |

## Plattform- und Betriebsfunktionen

| Fähigkeit | Status | Nachweis und Grenze |
| --- | --- | --- |
| REST API und .NET SDK für Deployment/Start | `supported` | REST startet den persistenten BPMN-Subset bis End-/Wait-State; die zentralen Verträge laufen über einen echten API-Host. |
| gRPC | `partial` | Vertrags-/Smoke-Tests existieren; keine gleichwertige Abdeckung des freigegebenen persistenten Kernpfads. |
| EF-Core-Runtime-Persistenz | `supported` | Instanzen, Tokens, Variablen, Tasks, Jobs, Subscriptions, Incidents, Inbox, Outbox und Worker-Registrierungen sind relational modelliert und migriert. |
| Produktionskonfiguration und Mandantenschutz | `supported` | Production/Stage verlangen Connection Strings und persistenten Data-Protection-Keyring; Fake/InMemory/NoOp-Auflösungen und unsichere Script-/Connector-/Plug-in-Konfigurationen brechen den Start ab. |
| Externe Brokerzustellung der Outbox | `supported` | Der persistente Publisher least Nachrichten atomar und replika-sicher und liefert mit stabilen Message-IDs, Retry-/Dead-Letter-Semantik und Broker-Readiness an RabbitMQ oder Kafka. Echte RabbitMQ-/PostgreSQL-Akzeptanztests sind ein verpflichtendes separates CI-Gate. |
| Process Mining / Analytics | `partial` | Ein persistenter Sink und dauerhafte Runtime-Outbox-Projektion sind registriert; Management- und Prometheus-Ausgaben lesen persistente Betriebszähler. Eine vollständige fachliche Analytics- und Process-Mining-Qualifizierung ist nicht nachgewiesen. |
| bpmn.io Studio | `partial` | Gepinnte Assets und Moddle-Tests existieren; vollständige Modell-/Runtime-Parität ist nicht nachgewiesen. |

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

Die sieben Phase-1-Testmethoden tragen `Category=Phase1Acceptance`; sechs zusätzliche Phase-2-Verträge tragen `Category=Phase2Acceptance`. Nicht aufgeführte BPMN-Semantik ist nicht automatisch unterstützt.
