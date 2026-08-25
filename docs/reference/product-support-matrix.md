# Produkt-Support- und Acceptance-Matrix

**Stand:** 25.08.2026
**Geltungsbereich:** aktueller `master`; bewertet wird der öffentlich nutzbare End-to-End-Pfad, nicht nur das Vorhandensein einzelner Klassen oder Unit-Tests.

## Statusdefinitionen

| Status | Verbindliche Bedeutung |
| --- | --- |
| `supported` | Über die öffentliche API end-to-end implementiert, dauerhaft gespeichert und durch einen erfolgreichen Vertragstest belegt. |
| `partial` | Parser-, Modell-, Komponenten- oder API-Teile sind vorhanden, aber mindestens ein erforderlicher End-to-End-Pfad oder Wiederanlaufnachweis fehlt. |
| `unsupported` | Nicht implementiert, nur Platzhalter/No-op oder ohne belastbaren Ausführungsnachweis. Darf nicht als produktionsfähig beworben werden. |

## BPMN

| Fähigkeit / Elementgruppe | Status | Aktueller Nachweis und Grenze |
| --- | --- | --- |
| XML-Parsing und Modellvalidierung | `partial` | Breite Parser- und MIWG-Referenztests existieren. `RepositoryService` verwendet jedoch nicht den sicheren BPMN-Parser und akzeptiert bei XML-Fehlern den Dateinamen als Process-Key. |
| Serialisierung und Diagramm-Roundtrip | `partial` | Strict-Roundtrip- und Ecosystem-Tests existieren; vollständige verlustfreie Interoperabilität für alle BPMN-DI- und Vendor-Elemente ist nicht nachgewiesen. |
| Deployment und Definition-Persistenz | `partial` | `POST /api/repository` persistiert XML relational. Validierung und atomare, mandantensichere Versionierung fehlen; `Version` ist fest auf `1` gesetzt. |
| None Start/End Event und Sequence Flow | `partial` | Direkte Engine-Tests existieren. Der öffentliche Runtime-Start erzeugt nur einen `ProcessInstance`-Datensatz und startet keinen persistenten Token-Flow. |
| Service Task | `partial` | Handler und direkte Engine-Ausführung existieren. Deploy/Start über die API ruft den Handler nicht als Teil derselben Runtime auf. |
| User Task: Erzeugen, Claim, Complete, Resume | `partial` | Task-CRUD-Endpunkte und Persistenzservice existieren. Der Runtime-Start erzeugt keine User Task und Task-Completion setzt den Prozessfluss nicht fort. |
| Exclusive/Parallel/Inclusive/Event-based Gateway | `partial` | Parser und direkte Engine-Logik sind vorhanden. Ein persistenter API-End-to-End-Nachweis einschließlich Join-, Condition- und Restart-Semantik fehlt. |
| Subprozess, Event-Subprozess, Call Activity, Multi-Instance | `partial` | Modell- und direkte Engine-Tests existieren. Persistente Lebenszyklen und Wiederanlauf sind nicht nachgewiesen. |
| Timer Start/Catch/Boundary und Job-Ausführung | `unsupported` | Timer-/Job-Komponenten existieren, aber kein bestandener API-Test belegt persistentes Warten, Fälligkeit, Fortsetzung und Neustart. |
| Message-/Signal-Catch und Correlation | `unsupported` | API-Methoden existieren; Signal ist im Runtime-Service ein No-op und Message-Correlation aktualisiert nur Variablen ohne Subscription-/Token-Fortsetzung. |
| Error, Escalation, Compensation, Cancel, Terminate | `unsupported` | Parser-/Direktlogik ist teilweise vorhanden; belastbare persistente Scope-, Boundary- und Recovery-Semantik fehlt. |
| Prozessneustart, parallele Instanzen und Idempotenz | `unsupported` | Kein End-to-End-Nachweis für persistente Tokens, Optimistic Concurrency, Lease/Locking oder deduplizierte Fortsetzung. |

## DMN

| Fähigkeit | Status | Aktueller Nachweis und Grenze |
| --- | --- | --- |
| Decision-Table-Parsing | `partial` | Parser- und API-Tests existieren; keine offizielle DMN-TCK als Release-Gate. |
| Hit Policies | `partial` | Unit-/Integrationstests decken mehrere Policies ab; vollständige Standardsemantik und Aggregationen sind nicht als End-to-End-Vertrag belegt. |
| FEEL | `partial` | Ein Ausdrucks-Subset ist implementiert. Vollständige FEEL-Konformität wird nicht behauptet. |
| DRD, Abhängigkeiten und BusinessRuleTask-Integration | `partial` | Einzelne Modelle und Integrationspfade existieren; ein durchgängiger persistenter BPMN/DMN-Runtime-Nachweis fehlt. |

## CMMN

| Fähigkeit | Status | Aktueller Nachweis und Grenze |
| --- | --- | --- |
| CMMN-Parsing und Definition-API | `partial` | Parser und API-Verträge können einfache Definitionen annehmen und lesen. |
| Human/Process/Case Tasks, Stages und Milestones | `partial` | Domänen- und Engine-Komponenten existieren; kein vollständiger öffentlicher Case-Lifecycle ist nachgewiesen. |
| Sentries, Entry/Exit Criteria und Discretionary Items | `unsupported` | Keine verbindliche Conformance-Suite und kein persistenter End-to-End-Nachweis. |
| Case File Items und Wiederanlauf | `unsupported` | Dauerhafte Zustandspropagierung und Restart-Verhalten sind nicht belegt. |

## Plattform- und Betriebsfunktionen

| Fähigkeit | Status | Aktueller Nachweis und Grenze |
| --- | --- | --- |
| REST API und .NET SDK für Deployment/Start | `partial` | Verträge und Integrationstests existieren; Start bedeutet derzeit noch nicht BPMN-Ausführung bis zu einem End- oder Wait-State. |
| gRPC | `partial` | Vertrags-/Smoke-Tests existieren; keine gleichwertige Abdeckung des produktiven Kernpfads. |
| EF-Core-Persistenz | `partial` | SQLite, PostgreSQL und SQL Server sind konfigurierbar. Dauerhafte Runtime-Tokens, Subscriptions, Jobs und Incidents fehlen. |
| Process Mining / Analytics | `partial` | Events und Endpunkte existieren; atomare Kopplung an Runtime-Zustandsänderungen und vollständige Betriebsmetriken fehlen. |
| bpmn.io Studio | `partial` | Gepinnte Assets und Moddle-Tests existieren; vollständige Modell-/Runtime-Parität ist nicht nachgewiesen. |

## Verbindliche Acceptance-Fälle

Alle Fälle müssen einen echten `WebApplicationFactory`-Host und eine relationale SQLite-Testdatenbank verwenden. Parser-only-Tests, gemockte HTTP-Responses und bloße `Count > 0`-Assertions erfüllen diese Verträge nicht.

| ID | Vertrag | Erwarteter Endzustand | Status |
| --- | --- | --- | --- |
| `P1-AC-01` | Deploy → Start → Service Task → User Task → Complete → End | Handler wurde ausgeführt; User Task ist persistent; nach Complete ist die Instanz `Completed` und hat keine aktiven Tokens/Tasks. | **rot** – Vertragstest vorhanden, Runtime startet den BPMN-Flow noch nicht. |
| `P1-AC-02` | Timer Catch/Boundary → Wait → Due → Resume | Timer und Subscription sind persistent; vor Fälligkeit kein Fortschritt, danach genau eine Fortsetzung. | **rot** – Catch- und Boundary-Verträge vorhanden; der öffentliche Runtime-Start persistiert weder Wait-Token noch die angehängte User Task. |
| `P1-AC-03` | Message und Signal → Wait → Correlate/Broadcast → Resume | Korrelation trifft nur passende aktive Subscription und setzt den Token genau einmal fort. | **rot** – getrennte Message- und Signal-Verträge vorhanden; die Runtime persistiert die erforderlichen Subscription-Wait-Token nicht. |
| `P1-AC-04` | Prozess-/Host-Neustart während Wait-State | Neue Hostinstanz lädt Zustand und setzt ohne Verlust oder Duplikat fort. | **rot** – Vertrag verwendet zwei echte API-Hosts und dieselbe relationale SQLite-DB; bereits die dauerhafte Wait-State-Vorbedingung fehlt. |
| `P1-AC-05` | Parallel Gateway und mehrere Instanzen | Join wartet auf alle erforderlichen Tokens; Instanzen beeinflussen einander nicht. | **rot** – Vertrag startet zwei Instanzen und prüft Branch-/Join-Isolation; der Runtime-Start erzeugt die parallelen Task-Zweige nicht. |

Alle sieben Testmethoden für `P1-AC-01` bis `P1-AC-05` tragen den Trait `Category=Phase1Acceptance`. `scripts/verify-phase1-acceptance-baseline.sh` führt jeden Vertrag einzeln aus und akzeptiert nur den jeweils dokumentierten fachlichen Fehler. Sobald Phase 2 einen Pfad implementiert, wird der entsprechende Vertrag zum blockierenden Green-Gate; seine Assertions werden dafür nicht abgeschwächt.
