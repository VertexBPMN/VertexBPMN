# External Tasks und Agent-Vertragsprüfung – A00 Inventur und Baseline

Stand: 2026-09-12
Basiscommit: `9f65335a45e61b50d421b05850e0e8115dc69d65`
Branch: `codex/external-agent-a00-inventory`
Status: **A00 abgeschlossen; noch keine External-Task-Funktion implementiert.**

## 1. Ergebnis

VertexBPMN besitzt belastbare Grundlagen für persistente Prozessausführung, relationale Transaktionen, Outbox-/Inbox-Deduplizierung und bedingtes Leasing. Es existiert jedoch noch **kein External-Task-Protokoll**: keine External-Task-Definition, keine atomare Job-/Wait-Erzeugung, keine Claim-/Heartbeat-/Complete-/Fail-API, kein Lease-Fencing und keine idempotente Completion mit Prozessfortsetzung.

Der neue Task-Typ darf deshalb nicht als bereits unterstützter „External Worker“ dargestellt und nicht auf den vorhandenen normalen Service-Task-Pfad abgebildet werden. Zielruntime für A02–A04 ist ausschließlich `PersistentProcessExecutionRuntime`. `ProcessEngine` und `DistributedProcessEngine` müssen den neuen Task-Typ bis zu einer vollständigen Implementierung explizit ablehnen.

## 2. Funktionsmatrix

| Funktion | Bestehender Code | Wiederverwendung/Erweiterung | Aktueller Nachweis | Offene Grenze |
|---|---|---|---|---|
| BPMN-Service-Task-Modell | `src/VertexBPMN.Domain/Model/Bpmn/BpmnModel.cs:48`, `src/VertexBPMN.Engine/Parsing/BpmnParser.cs:493` | `BpmnTask` und Vertex-Extension-Konvention erweitern | Parser-/Strict-Roundtrip in der A00-Auswahl grün | `vertex:externalTask`, Topic, Profil und Limits fehlen |
| Parser/Serializer/Studio-Roundtrip | `src/VertexBPMN.Engine/Parsing/VertexBpmnExtensions.cs`, `src/VertexBPMN.Engine/Serialization/BpmnSerializer.cs`, `src/VertexBPMN.Studio/tools/bpmn-io/src/vertex.json` | Bestehenden Namespace und Extension-Element-Pipeline nutzen | Bestehende Parser-/Serializer-Tests grün | Kein Moddle-Typ, Properties-Provider oder Validator für External Tasks |
| Persistenter Aktivitäts-Wait | `src/VertexBPMN.Engine/Execution/PersistentProcessExecutionRuntime.cs` | Token-, Scope-, Boundary- und Multi-Instance-Helfer nutzen; Job und Wait in derselben Transaktion speichern | `PersistentRuntimePhase2AcceptanceTests` grün | Service Tasks werden derzeit inline ausgeführt; keine `ActivityExecutionId`-gebundene Wartestelle |
| Simple Engine | `src/VertexBPMN.Engine/Execution/ProcessEngine.cs:805` | Nur explizite Capability-Prüfung/Ablehnung ergänzen | Codeprüfung | Unbekannter Handler erzeugt aktuell ein künstliches Default-Ergebnis und der Token läuft weiter (`:826–935`) |
| Legacy Distributed Engine | `src/VertexBPMN.Engine/Execution/DistributedProcessEngine.cs:1498` | Nur explizite Capability-Prüfung/Ablehnung ergänzen | Codeprüfung | Dispatch wird nicht als persistenter Wait behandelt; die Engine setzt direkt mit dem nächsten Knoten fort (`:1513–1538`) |
| Bestehende Timer-/Handler-Jobs | `src/VertexBPMN.Domain/Entities/Job.cs`, `src/VertexBPMN.Application/JobExecutorService.cs:69`, `src/VertexBPMN.Infrastructure/Persistence/Repositories/JobRepository.cs` | CAS-/Revision-Muster als Ausgangspunkt verwenden, External Tasks fachlich getrennt modellieren | Codeprüfung | Kein Lease-ID/Fencing, Heartbeat, Completion-ID, Ergebnishash, Deadline oder Aktivitätsdurchlauf; Polling nicht paginiert |
| Atomare Runtime-Transaktion | `PersistentProcessExecutionRuntime` und `BpmnDbContext` | Bestehende relationale Transaktionsgrenze für Job + Wait + Historie verwenden | Persistente Runtime-Akzeptanztests grün | Langlaufende Handler werden aktuell innerhalb des Runtime-Pfads aufgerufen; Netzwerk-/LLM-Aufrufe müssen herausgelöst werden |
| Outbox | `src/VertexBPMN.Infrastructure/Messaging/RuntimeOutboxPublisherService.cs:51–176` | Begrenztes Batch-Polling, Lease-Muster, Retry/Dead-Letter und stabile Ereignis-IDs wiederverwenden | Phase-4-Abnahmedokumentation und Codeprüfung | Lock-Owner ist kein Lease-Fencing; eine Outbox-Zeile allein implementiert noch keine idempotente Prozessfortsetzung |
| Inbox/Deduplizierung | `src/VertexBPMN.Infrastructure/Messaging/RuntimeInboxConsumerService.cs:157–213`, Unique-Index in `BpmnDbContext.cs:561` | Unique-Key-Muster für serverseitige Completion-Deduplizierung übernehmen | Phase-2-/Phase-4-Abnahmedokumentation und Codeprüfung | Consumer darf nicht unverändert übernommen werden; Claim-/Ack-Crashfenster und Completion-Autorisierung benötigen einen eigenen Vertrag |
| Worker-Verwaltung | `src/VertexBPMN.Api/Controllers/LoadBalancerController.cs`, `WorkerNodeManager`, `PersistentWorkerNodeManager` | Betriebsmetrik/Registrierung höchstens ergänzend nutzen | Codeprüfung | Das ist eine Node-/Capacity-Control-Plane, keine sichere External-Task-Datenebene; Body-Worker-ID ist keine Identität |
| Job-Lese-API | `src/VertexBPMN.Api/Controllers/VertexJobController.cs:13` | Tenant-Auflösungskonvention nutzen | Security-Codeprüfung | Nur read-only Due-Job-Liste; keine eingeschränkte Worker-Policy und keine Topic-/Profilberechtigungen |
| Authentifizierung/Autorisierung | `src/VertexBPMN.Api/Security/SecurityConfiguration.cs:122–166` | Bestehende JWT/API-Key-Normalisierung und Tenant-Prüfungen nutzen | Tenant-Security-Auswahl grün | Eigene `ExternalTaskWorker`-Policy sowie Tenant-, Topic- und Profilclaims fehlen |
| AI-Service-Tasks | `src/VertexBPMN.Application/Handlers/AIServiceTaskHandler.cs`, `GenericAiServiceTaskHandler.cs:45` | Nur klar getrennte Interfaces/Provider-Muster prüfen | `GenericAiServiceTaskHandlerTests` grün | Generic-Handler ist ausdrücklich ein Mock; realer Handler läuft inline und erlaubt zu breite modellseitige Konfiguration |
| Agent-Vertragsprüfer | Nicht vorhanden | In A05 auf generischem External-Task-Kern aufbauen | Kein Realnachweis | Dokumentstore, Runtime/Modell, Profil, Schema, fachliche Kategorien und Güteschwellen sind offen |
| Crash-/Race-Sicherheit | `tests/VertexBPMN.Tests/PersistentRuntimePhase2AcceptanceTests.cs`, `Phase4OutageAcceptanceTests.cs`, `Phase5RecoveryAcceptanceTests.cs` | Reale PostgreSQL-/RabbitMQ-Testmuster und getrennte DbContexts wiederverwenden | Dokumentierte Phase-4-Ausfalltests vorhanden | Keine External-Task-Races; kontrollierte Barrieren/Fault-Points und echte Prozessabbrüche fehlen |

## 3. Ausführungsmodi und vorläufiger Supportvertrag

| Modus | Aktuelles Verhalten | Entscheidung für A01/A02 |
|---|---|---|
| `PersistentProcessExecutionRuntime` | Persistente Tokens, Tasks, Jobs, Subscriptions, Historie und relationale Transaktionen | **Zielmodus.** External Task erzeugt Job und Wait atomar und wird erst nach dauerhaft angenommener Completion fortgesetzt. |
| `ProcessEngine` (`Simple`) | Service-Task-Handler synchron/blockierend; fehlender Handler fällt auf simuliertes Default-Ergebnis zurück | **Nicht unterstützt.** Deployment/Execution eines `vertex:externalTask` muss fail-closed ablehnen. Kein Default-Ergebnis. |
| `DistributedProcessEngine` (Legacy) | Lokaler Handler oder Message-Dispatch; anschließend unmittelbare Fortsetzung | **Nicht unterstützt.** Explizite Ablehnung, bis derselbe persistente Lease-/Completion-Vertrag vollständig implementiert ist. |
| CMMN-Pfad | Eigenständige Plan-Item-Ausführung ohne External-Task-Vertrag | Nicht Teil des ersten Umfangs; kein implizites Mapping. |

Diese Einschränkung ist keine dauerhafte Produktzusage, sondern die sicherheitsrelevante Startgrenze. Zusätzliche Modi können erst mit denselben E01–E11-Nachweisen freigegeben werden.

## 4. Wiederverwendungsentscheidungen

### Wiederverwenden und gezielt erweitern

- Providerkonfiguration und EF-Mappings aus `BpmnDbContext`, einschließlich getrennter PostgreSQL-/SQLite-Behandlung.
- Revision/CAS als Grundmuster aus `JobRepository.TryLeaseAsync`; die External-Task-Operationen erhalten jedoch eigene SQL-Prädikate, Lease-ID/Fencing-Generation und Rückgabeverträge.
- Begrenztes Polling, Backoff, Leasing und Dead-Letter-Prinzipien aus der Runtime-Outbox.
- Unique-Key-/Idempotency-Muster aus Runtime-Inbox und `SourceEventId`-Projektion.
- Persistente Runtime-Helfer für Scope, lokale Variablen, Boundary Events und Multi-Instance.
- Bestehende Tenant-Auflösung und Auth-Claim-Normalisierung, ergänzt um eine engere Worker-Policy.
- Reale Ausfall-/Mehrreplikat-Testinfrastruktur aus Phase 4 und Recovery-Muster aus Phase 5.

### Nicht unverändert wiederverwenden

- `Job` als fertiges External-Task-Modell: zentrale Identitäts-, Lease-, Deadline-, Attempt- und Dedup-Felder fehlen.
- `IJobRepository.TryLeaseAsync` als Protokoll: das Default-Interface mutiert nur das Objekt und behauptet Erfolg; es ist keine atomare Datenbankgarantie.
- `JobExecutorService` als External Worker: es führt Handler im Serverprozess aus, verwendet `DateTime.UtcNow`, hat keine Heartbeats und löscht erfolgreiche Jobs.
- `LoadBalancerController`/Worker-Registrierung als Worker-Authentifizierung.
- `GenericAiServiceTaskHandler` als Agent-Runtime oder Realnachweis; er ist ein Mock.
- Den breit konfigurierbaren `AIServiceTaskHandler` als sicheren Vertragsprüfer.
- Runtime-Inbox-Consumer als fertige Completion-Fortsetzung; dessen Zustell-/Ack-Vertrag ist dafür nicht ausreichend belegt.

## 5. Sicherheits- und Atomizitätsbefund

1. Worker-Identität muss aus dem authentifizierten Principal stammen. `workerId`, Tenant, Topic und Profil im Request sind Filterdaten, keine Autorität.
2. Jede erfolgreiche Übernahme braucht eine neue nicht erratbare Lease-ID plus monotone Generation. Heartbeat, Complete und Fail vergleichen Job, Tenant, Subject, Lease und Generation atomar.
3. Job, wartender Aktivitätsdurchlauf und initiale Historie müssen in einer Transaktion entstehen. Ein Unique-Key muss `ActivityExecutionId` beziehungsweise den tatsächlichen Durchlauf enthalten, damit Retry keine Duplikate und Schleifen/Multi-Instance keine falschen Kollisionen erzeugen.
4. Completion darf erst nach dauerhaftem Ergebnis-/Dedup-/Fortsetzungszustand bestätigt werden. Prozessfortsetzung darf bei Wiederanlauf nur einmal wirksam werden.
5. Absolute Deadline und Attempt-Budget werden serverseitig mit einer injizierten Zeitquelle geprüft; Worker-Heartbeats dürfen die Deadline nicht verschieben.
6. Nur explizit gemappte Eingaben verlassen die Engine. Dokumente, Prompts, Modellantworten und Fehler werden nicht standardmäßig geloggt.

## 6. Kontrollpunkte für E01–E14

Für deterministische Tests sind folgende testbare Nahtstellen erforderlich:

- `TimeProvider` für Claim, Lease, Heartbeat, Backoff und Deadline; keine zufälligen Sleeps.
- Barriere direkt nach Erzeugung von Job + Wait, aber vor Commit, sowie direkt nach Commit.
- Barriere vor und nach dem Claim-CAS mit zwei echten DbContexts/Worker-Principals.
- Fault-Point vor Ergebnisannahme, nach Ergebnis-/Dedup-Speicherung und vor/nach Fortsetzungsdispatch.
- Fault-Point für konkurrierendes interrupting Boundary Event, Prozessabbruch und Completion.
- Getrennte Prozesse für mindestens die Crashfälle E06/E11; Exceptions im selben Prozess sind kein Crashnachweis.
- PostgreSQL-Nachweise für CAS, Indizes, Recovery und Mehrreplikat-Races; SQLite-Nachweis für zugesagtes lokales Verhalten und Migrationen.
- Autorisierungsnegativtests pro Route: Fremdtenant, fremdes Topic/Profil, falscher Worker-Subject, veraltete Lease und reine Body-Worker-ID.
- Studio-Roundtrip/Undo/Redo lokal gegen das reale Studio; keine CI-Pflicht und kein Mock-Ergebnis als E14-Abnahme.

## 7. Baseline

### Erfolgreich

Ausgeführt gegen die vorhandene Release-Testassembly:

```powershell
dotnet tests/VertexBPMN.Tests/bin/Release/net10.0/VertexBPMN.Tests.dll `
  -class '*PersistentRuntimePhase2AcceptanceTests' `
  -class '*UnifiedParserSerializerTests' `
  -class '*StrictSerializerRoundtripTests' `
  -class '*GenericAiServiceTaskHandlerTests' `
  -class '*TenantIsolationPhase3SecurityTests' `
  -class '*Phase2ProductionConfigurationTests' `
  -parallelMode none
```

Ergebnis: **39 Tests, 39 bestanden, 0 fehlgeschlagen, 0 übersprungen**, 11,086 Sekunden.

### Umgebung blockiert frischen Build

```powershell
dotnet build VertexBPMN.sln --configuration Release --no-restore --disable-build-servers --maxcpucount:1
```

Der Build konnte nicht frisch erzeugt werden, weil MSBuild temporäre Dateien in vorhandenen `obj/Release/net10.0`-Ordnern von CLI und SDK nicht schreiben durfte (`MSB3491`/`MSB3101`, Access denied). Zusätzlich wurden drei bereits vorhandene Analyzerwarnungen in Performance-/UI-Tests sichtbar. Deshalb ist die grüne 39er-Auswahl ein Regressionstest der vorhandenen Assembly, **kein sauberer Checkout-/Fresh-Build-Nachweis**. Die lokale Rechteursache ist unabhängig vom External-Task-Feature zu beheben oder der Build in einem sauberen Checkout zu wiederholen.

## 8. Verbindliche Eingaben für A01

A01 muss vor Implementierung mindestens festschreiben:

1. XML-Vertrag für `vertex:externalTask`, Topic, Profilreferenz, Input-/Output-Mapping, Attempts und Deadline.
2. Vollständige Zustands-/Operationsmatrix samt HTTP-Status und stabilen Fehlercodes.
3. `ActivityExecutionId`-Semantik für normale, Loop-, Scope- und Multi-Instance-Durchläufe.
4. Worker-Claims und eigene Policy für Tenant, Topic und Profil; keine Admin-Abkürzung als Produktionsvertrag.
5. CAS-/Fencing-Prädikate, serverseitige Zeit, Completion-ID/Ergebnishash und atomare Fortsetzungsgrenze.
6. Dokumentzugriff, Audit-Redaktion und Aufbewahrung. Diese Produktentscheidung bleibt für A05 offen, blockiert aber den generischen Kernvertrag nicht.
7. Explizite Ablehnung in Simple/Legacy-Distributed, einschließlich Deploymentvalidator und Tests.

## 9. A00-Abnahme

Die geforderte Matrix, Engine-Abgrenzung, Wiederverwendungsentscheidung, Baseline und Crash-/Race-Kontrollpunkte liegen vor. A00 ist damit dokumentarisch abgeschlossen. Dies ist **keine Implementierung und keine Produktionsfreigabe**. Nächster freizugebender Schritt ist A01 – verbindlicher External-Task-/Security-Vertrag.
