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
| `ServiceTaskDispatch` | D | ✓ (Engine reicht `token.ProcessInstanceId`) | tenant-optional | `targetWorkerId, implementation, attributes, variables` | Outbox→Broker; **kein registrierter Inbox-Handler i. Prod** | Service-Task über HTTP-External-Task (AgentWorker), nicht über Broker |
| `AiTaskDispatch` | D | ✓ (optional `processInstanceId`) | tenant-optional | `targetWorkerId, aiProvider, aiModel, attributes, variables` | Outbox→Broker; **kein registrierter Inbox-Handler** | s. o. |
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

1. **Tenant-/Korrelations-Gap (ServiceTaskDispatch/AiTaskDispatch)** — ✅ **behoben** (Commit unten, § 6): die beiden Dispatch-Methoden akzeptieren jetzt optional `processInstanceId`+`tenantId`; die Engine reicht `token.ProcessInstanceId` durch. `PersistentMessageDispatcher` schreibt beide in die Outbox-Zeile, sodass das Draht-Envelope (`{id,eventType,processInstanceId,tenantId,...}`) die Korrelation trägt. Tenant wird weiterhin verfügbar-optional gesetzt (die Distributed-Engine besitzt keinen Tenant-Kontext).
2. **Kein produktiver Inbox-Geschäftshandler** — bewusste, sichere Absicherung (Reject statt Fake-Success). Für Azure freigegebene Ereignistypen ist ein dokumentierter Verarbeitungsweg erforderlich; solange nur Weiterleitung/Historisierung genutzt wird, gilt: Maximal-Abnahme für Broker-Fachkonsumption erst nach expliziter Handler-Anbindung.
3. **Extern/Stage (E2E am Ende, wie besprochen):** HTTP-External-Task-Lease/-Completion/-Recovery gegen `VertexBPMN.AgentWorker` auf Azure; E2E-Sende-/Empfangs-/DLQ-Lauf gegen echten Service-Bus-Namespace; Phase4-Suiten gegen echte RabbitMQ.

## 6. Korrektur (umgesetzt)

`ServiceTaskDispatch`/`AiTaskDispatch` um Prozessinstanz + Tenant angereichert. Rückwärtskompatibel: beide `Dispatch*Async`-Methoden in `IMessageDispatcher` erhalten optionale Parameter `Guid? processInstanceId = null, string? tenantId = null` (Default unverändert → `Guid.Empty`/`null`, wie bisher). Alle Implementierungen (`PersistentMessageDispatcher`, `InMemory`, `NoOp`, `RabbitMq`, `Kafka`) wurden angepasst; die Engine reicht `token.ProcessInstanceId` am Service-Task-Dispatch durch. `PersistentMessageDispatcher` schreibt `ProcessInstanceId`/`TenantId` in die Outbox-Zeile → Draht-Envelope trägt die Korrelation. Kein direkter Broker-Dispatcher eingeführt. Verifikation: `P3EventContractTests` (Service- + AiTask-Dispatch setzen die Korrelation/Tenant; stabile `MessageId` = Outbox-Id).

## 7. Semantische Envelope-Konformitätsprüfung (TypeSafe System One)

Auf Wunsch von Yova wird für P3 der **TypeSafe**-Skill eingesetzt: Ein eingehendes Outbox-Envelope wird im Inbox-Pfad zusätzlich zu den deterministischen Checks (Envelope-Version, Idempotenz, Tenant) semantisch gegen seinen deklerierten Ereignisvertrag geprüft — als Choice-Judgment von TypeSafe System One (`jev-latest`), nicht als Freitext-LLM.

- **Komponenten (Infrastructure/Messaging):** `TypeSafeConformanceOptions`, `ITypeSafeConformanceClient`/`TypeSafeConformanceClient` (POST `/v1/systemone`, Antwort-`choice`/`confidence`/`probabilities`), `ITypeSafeEnvelopeConformanceValidator`/`TypeSafeEnvelopeConformanceValidator` (State = `{eventType, processInstanceId, tenantId, declaredContract, payload}`, Choice-Kriterien `conforms|malformed|wrong_contract|unclear`).
- **Verdict-Mapping:** `conforms`→weitermachen; `malformed`/`wrong_contract`→`Rejected` (DLQ); `unclear` (Konfidenz < Schwelle) →`RetryableFailure` (Requeue/Review); Client nicht erreichbar/absent → `NotEvaluated` (**fail-open**, ein Prüfdienst-Ausfall blockiert die Fachverarbeitung nicht).
- **Inbox-Anbindung (RuntimeInboxProcessor, Schritt 2b):** librarieregeln via `scope.ServiceProvider.GetService<ITypeSafeEnvelopeConformanceValidator>()` — **nur aktiv, wenn registriert UND `Runtime:TypeSafeConformance:Enabled=true`**; sonst `NotEvaluated` und der bestehende Pfad bleibt byteidentisch. Aktivierung + `ApiKey` erfolgen ausschließlich server-seitig (niemals in Repos/Clients); standardmäßig deaktiv.
- **Tests:** `TypeSafeConformanceTests` (deterministisch, Fake-Client: Verdict-Mapping, Disabled, Low-Confidence→Unclear, Fail-open, Prozessor-Integration Malformed/WrongContract→Rejected, Unclear→Retryable, Conforms/NotEvaluated→Completed). `TypeSafeConformanceAcceptanceTests` (live gegen echte TypeSafe-API, `Category=ContractReviewTypeSafe`, ohne `TYPESAFE_API_KEY` übersprungen).
- **Grenze (ehrlich):** TypeSafe akzeptiert nur Text; die Payload wird als JSON in den State gegeben. Es ist ein externer, kostenpflichtiger Dienst — Produktionsaktivierung erfordert explizite Freigabe (Budget/Latency/Nicht-Determinismus) in P5–P8.





