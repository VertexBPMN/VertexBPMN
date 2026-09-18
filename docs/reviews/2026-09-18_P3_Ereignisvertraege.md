# P3 – Ereignisverträge der Runtime-Outbox/Inbox (Inventar + Absicherung)

**Stand:** 18. September 2026
**Repo:** `VertexBPMN` (branch `master`)
**Ziel:** Jeder Ereignistyp, der aus der Engine in die dauerhafte Runtime-Outbox geschrieben wird, ist dokumentiert (Schema, Tenant-Kontext, Routing, Konsument, fachlicher Abschluss). `ServiceTaskDispatch`/`AiTaskDispatch` sind auf vollständige Tenant-/Korrelationsdaten geprüft. HTTP-External-Tasks (AgentWorker) bleiben vom Service-Bus-Pfad entkoppelt.

## 1. Transportpfad (verifiziert im Code)

- Production registriert `IMessageDispatcher` → **`PersistentMessageDispatcher`** (`InfrastructureModule.cs:53`). KEIN direkter Broker-Dispatcher (z. B. `AzureServiceBusMessageDispatcher`) existiert oder wird registriert.
- Der Dispatcher schreibt atomar eine **Pending**-Zeile in `RuntimeOutboxMessage` (DB). Erst der separate, dauerhafte **`RuntimeOutboxPublisherService`** leased die Zeile und sendet über den Transport (`RabbitMq`/`Kafka`/`AzureServiceBus`) mit stabiler `MessageId` = Outbox-Id (`N`).
- Das Draht-Envelope (alle Transporte, z. B. `RabbitMqRuntimeOutboxTransport.cs`) ist:
  `{ id, eventType, processInstanceId, tenantId, occurredAt, payload }` — Routing-Key = `EventType` (RabbitMQ Topic `Runtime:Outbox:Destination`; ASB Topic-Subscription bzw. Queue).
- Der Inbox-Konsument (`RuntimeInboxConsumerService` / `AzureServiceBusRuntimeInboxConsumerService`) verarbeitet über den **providerneutralen `RuntimeInboxProcessor`** idempotent (Unique `(TenantScope, Operation, IdempotencyKey)`), bei **genau einem** fachlichem Abschluss.

## 2. Inventar der Ereignistypen

Legende Producer: **D** = `PersistentMessageDispatcher`, **R** = `PersistentProcessExecutionRuntime.AddOutbox`, **W** = `PersistentWorkerNodeManager`, **M** = `LiveProcessMigrationService`.

| EventType | Prod | ProzessInstanz | Tenant | Payload (gekürzt) | Bestimmungsort / Consumer | Fachl. Abschluss |
|---|---|---|---|---|---|---|
| `ServiceTaskDispatch` | D | **Guid.Empty** ⚠️ | **null** ⚠️ | `targetWorkerId, implementation, attributes, variables` | Outbox→Broker; **kein registrierter Inbox-Handler i. Prod** | Service-Task über HTTP-External-Task (AgentWorker), nicht über Broker ⇐ P3-Gap |
| `AiTaskDispatch` | D | **Guid.Empty** ⚠️ | **null** ⚠️ | `targetWorkerId, aiProvider, aiModel, attributes, variables` | Outbox→Broker; **kein registrierter Inbox-Handler** | s. o. ⇐ P3-Gap |
| `ExecutionTokenPublished` | D | token.ProcessInstanceId | null | `ExecutionToken` | Outbox→Broker | Ankündigungs-/Replay-Vertrag |
| `CaseTokenPublished` | D | Guid.Empty | null | `CaseToken` | Outbox→Broker | CMMN-Event-Ankündigung |
| `TaskQueued` | D | Guid.Empty | null | `taskId, taskType, variables` | Outbox→Broker | Queue-Ankündigung |
| `UserTaskDispatch` | D | Guid.Empty | null | `assignee, taskId, variables` | Outbox→Broker | Benachrichtigung (Fachwirkung liegt in DB) |
| `CaseFileUpdated` | D | Guid.Empty | null | `CaseFileUpdateEvent` | Outbox→Broker | CMMN-CaseFile-Event |
| `WorkerNotification` | W | Guid.Empty | null | `{ message }` | Outbox→Broker | Worker-Benachrichtigung, Best-effort |
| `ProcessStarted`, `ProcessCompleted`, `MessageCorrelated`, `SignalCorrelated`, `UserTaskCreated`, `UserTaskCompleted`, `ServiceTaskCompleted`, `ScriptTaskCompleted`, `BusinessRuleTaskCompleted`, `TimerFired`, `CallActivityStarted`, `CallActivityCompleted`, `IncidentCreated`, `IncidentRecovered`, `ProcessTerminated`, `CompensationTriggered` | R | instance.Id ✓ | instance.TenantId ✓ | typisiert (s. `AddOutbox`-Aufrufe) | Outbox→Broker | Historisierung/Analytics; Effekt in DB committet |
| `LIVE_MIGRATION_SNAPSHOT`, `ProcessMigrationRolledBack`, `ProcessMigrated` | M | betroffene Instanz | tenant | Migrationsdaten | Outbox→Broker | Live-Migration-Audit |

> ⚠️ **Korrelations-/Tenant-Gap (P3-Kernbefund):** `ServiceTaskDispatch`/`AiTaskDispatch` werden mit `Guid.Empty` und **ohne** `TenantId` angereichert (`PersistentMessageDispatcher.EnqueueAsync`). Tenant-Zuordnung und Prozesskorrelation fehlen damit im Draht-Envelope — ein Empfänger kann die Fachwirkung weder tenant-isoliert noch einer Prozessinstanz zuordnen.

## 3. Konsumenten-Status (verifiziert)

- **Kein `IRuntimeInboxHandler`/`IInboxEventSink` ist in Production registriert.** `@ApplicationModule`/`Api.Program` registrieren nur `IProcessMiningEventSink → WebhookEventSink` (ein ANDERER Vertrag). Der `RuntimeInboxProcessor` verweigert daher bei aktiver Broker-Inbox **jede** Quittierung ohne Handler (`Rejected`, → DLQ) — kein Fake-Success. Konsequenz: Die Broker-Uplink-Ereignisse (`RuntimeOutbox`→Broker→Inbox) haben in Production **derzeit keinen fachlichen Geschäftskonsumenten**; echte Fachwirkung passiert in der Engine selbst (DB-kommittete Effekte), der Broker-Pfad ist Weiterleitung/Historisierung.
- **HTTP-External-Tasks (AgentWorker)** laufen über einen eigenen, vom Broker getrennten Vertrag: `ExternalTaskJob`/`ExternalTaskAttempt`, `IExternalTaskLeaseService` (Claim/Heartbeat/Complete/Fail), `IExternalTaskRecoveryService` (Lease-Recovery), `IExternalTaskContractResolver` (Version/Schema). Studio nutzt `HttpExternalTaskOperationsService`. **Dieses Polling wird NICHT über die Service-Bus-Queue-Länge skaliert** (Planpunkt 4): Skalierung erfolgt über HTTP-Arbeitsvorrat und Lease-Zustand.
- **Dispatcher-Methoden ohne dauerhafte Semantik** (delegate-basiert) werden in `PersistentMessageDispatcher` bewusst mit `NotSupportedException` beantwortet: `DispatchDmnTaskAsync`, `SubscribeToMessageAsync`, `SubscribeToCaseFileUpdateAsync`. Kein Transport implementiert diese implizit (Planpunkt 5).

## 4. Absicherung (Tests, neu, grün)

`tests/VertexBPMN.Tests/Unit/Infrastructure/P3EventContractTests.cs` (3 Tests; SQLite in-memory, echter Dispatcher, Recording-Transport):
- `ServiceTaskDispatch_WritesDurableOutboxRow_ThenPublisherToBroker` — DB-Outbox zuerst (Pending, stabile Id, Payload-Treue), dann Publikation mit **gleicher** `MessageId` (= Outbox-Id), danach `Published`.
- `AiTaskDispatch_WritesDurableOutboxRow_ThenPublisherToBroker` — analog für `AiTaskDispatch`.
- `Dispatcher_IsDurable_NoBrokerDependency_AllMethodsWritePendingOutbox` — alle dauerhaften Dispatcher-Methoden erzeugen Pending-Rows **ohne** Broker-Transport; Registrierung `IMessageDispatcher → PersistentMessageDispatcher` (kein direkter Broker-Dispatcher). Bereits vorhanden zusätzlich: `Phase2ProductionConfigurationTests` (DI: `PersistentMessageDispatcher`), `RuntimeInboxProcessorTests`, `Phase4OutageAcceptanceTests.P4_AC_07` (echter RabbitMQ-Consumer exakt-einmal).

## 5. Befunde / offene Punkte

1. **Tenant-/Korrelations-Gap (ServiceTaskDispatch/AiTaskDispatch)** — zu beheben, sobald du die Schnittstellen-Erweiterung freigibst (Vorschlag in § 6).
2. **Kein produktiver Inbox-Geschäftshandler** — bewusste, sichere Absicherung (Reject statt Fake-Success). Für Azure freigegebene Ereignistypen ist ein dokumentierter Verarbeitungsweg erforderlich; solange nur Weiterleitung/Historisierung genutzt wird, gilt: Maximal-Abnahme für Broker-Fachkonsumption erst nach expliziter Handler-Anbindung.
3. **Extern/Stage (E2E am Ende, wie besprochen):** HTTP-External-Task-Lease/-Completion/-Recovery gegen `VertexBPMN.AgentWorker` auf Azure; E2E-Sende-/Empfangs-/DLQ-Lauf gegen echten Service-Bus-Namespace; Phase4-Suiten gegen echte RabbitMQ.

## 6. Vorgeschlagene Korrektur (wartet auf Freigabe)

`ServiceTaskDispatch`/`AiTaskDispatch` um Prozessinstanz + Tenant anreichern. Kleiner, sicherer Pfad: die beiden `Dispatch*Async`-Signaturen bleiben unverändert; der Korrelations-/Tenant-Kontext kommt aus der Engine mithilfe eines optionalen Aufrufs (`DispatchServiceTaskAsync(..., processInstanceId, tenantId)`-Overload bzw. Kontext via `AsyncLocal`/explizitem Parameter) und wird im Envelope gesetzt. Genaue API-Form in Absprache; kein direkter Broker-Dispatcher wird eingeführt.


