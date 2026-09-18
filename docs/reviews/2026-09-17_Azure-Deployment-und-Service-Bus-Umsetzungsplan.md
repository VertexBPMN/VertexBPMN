# Azure-Deployment und Azure-Service-Bus – Umsetzungsplan

**Stand:** 17. September 2026  
**Review:** Gegen den aktuellen Arbeitsstand geprüft; alle Implementierungspunkte sind noch offen. Aufwand S/M/L bezeichnet relative Größe, keine Lieferzusage.  
**Ziel:** VertexBPMN als sicher betreibbare, horizontal skalierbare Produktionsinstallation auf Microsoft Azure bereitstellen und Azure Service Bus als gleichwertigen Runtime-Messaging-Provider neben RabbitMQ und Kafka unterstützen.

## 1. Zielarchitektur und Abgrenzung

### Zielplattform

| Bereich | Zielservice | Begründung |
|---|---|---|
| Container-Images | Azure Container Registry (ACR) | Versionierte Images per Digest; keine Builds im Zielsystem. |
| Laufzeit | Azure Container Apps (ACA) | Getrennte Revisionen, HTTPS-Ingress, KEDA-Skalierung, Managed Identities und Container Apps Jobs ohne Kubernetes-Betriebsaufwand. |
| Persistenz | Azure Database for PostgreSQL Flexible Server | Verwaltete, hochverfügbare PostgreSQL-Instanz für die fünf Engine-Datenbanken und die Dependency Registry. |
| Messaging | Azure Service Bus | Durable Queues/Topics, Dead-Letter Queue, RBAC mit Managed Identity und betrieblich sichtbare Backlogs. |
| Secrets und Schlüssel | Azure Key Vault | Keine Secret-Werte in Git, IaC-Parametern, Logs oder Images. ACA darf Key-Vault-Referenzen als Laufzeit-Secrets/Umgebungsvariablen auflösen; deren Werte niemals ausgeben. |
| Schlüsselring | Azure Blob Storage, mit Key Vault geschützt | Gemeinsamer, dauerhafter ASP.NET-Core-Data-Protection-Key-Ring für replizierte API und Studio. |
| Telemetrie | Azure Monitor / Log Analytics / Application Insights | Zentralisierte Logs, Traces, Metriken und Alarmierung. |

Die öffentliche Eintrittsfläche ist zunächst ausschließlich Studio. Die API bleibt als interne Container App erreichbar; Studio ruft sie über den internen FQDN auf. Ein API-Ingress für externe Kunden wird nur eingerichtet, wenn ein versionierter API-Vertrag, Rate Limits und ein eigener Authentifizierungs-/WAF-Entscheid vorliegen.

### Bewusste Abgrenzung

- RabbitMQ und Kafka bleiben unterstützt; Azure Service Bus ersetzt sie nicht.
- Azure Service Bus ist **kein** RabbitMQ-kompatibler Endpunkt. Es wird ein eigener Adapter auf `Azure.Messaging.ServiceBus` implementiert; `RabbitMQ.Client` darf nicht gegen Service Bus konfiguriert werden.
- Der Plan umfasst Infrastruktur, Anwendung, Tests, Release-Prozess und Cutover. Er erzeugt noch keine Azure-Ressourcen.
- Im Produktionspfad ist `IMessageDispatcher` als `PersistentMessageDispatcher` registriert und schreibt bereits in die Runtime-Outbox. Service Bus wird darunter als Transport ergänzt. Kein zweiter direkter Publisher darf diesen dauerhaften Pfad umgehen. Der vorhandene AgentWorker verwendet HTTP-External-Tasks; er wird dadurch nicht automatisch ein Service-Bus-Consumer.

### Verbindliche lokale Kompatibilität

Azure ist ein zusätzliches Hostingprofil. Bestehende Aspire-Project-/Container-/ExternalServices-/WSLC-Profile, lokale PostgreSQL-/RabbitMQ-Installationen, SQLite und dateibasierte Schlüsselringe bleiben nutzbar. Auch selbst gehostetes Production/Stage wird nicht auf Azure gezwungen: dauerhafte, explizit konfigurierte lokale Alternativen bleiben zulässig. Azure-spezifische Prüfungen greifen nur bei gewähltem Azure-Hosting-/Providerprofil. Keine automatische Providerumschaltung durch `Production` oder installierte Azure-Pakete.

Provider für Registry, Schlüsselring und Messaging unabhängig konfigurieren. Bestehende Konfigurationsschlüssel weiter akzeptieren; neue Schlüssel und deren Vorrang dokumentieren. Lokaler Betrieb erfordert weder Azure-Anmeldung noch Docker: vorhandenes WSLC/ExternalServices bleibt verwendbar. Cloud-Tests sind explizit zuschaltbar.

## 2. Verifizierter Ist-Zustand

| Befund | Nachweis | Auswirkung |
|---|---|---|
| Produktions-Outbox erlaubt nur Kafka oder RabbitMQ. | `src/VertexBPMN.Infrastructure/InfrastructureModule.cs`, `ConfigureRuntimeOutbox` | Azure Service Bus kann heute nicht konfiguriert werden. |
| Die Outbox besitzt eine stabile Transportgrenze. | `src/VertexBPMN.Infrastructure/Messaging/IRuntimeOutboxTransport.cs` | Ein dritter Provider ist ohne Änderung der Prozess-Engine möglich. |
| Die Inbox ist direkt an RabbitMQ gebunden. | `src/VertexBPMN.Infrastructure/Messaging/RuntimeInboxConsumerService.cs` | Service Bus braucht einen eigenen Processor und eine transportneutrale Envelope-Verarbeitung. |
| Die Outbox hat Persistenz, Leasing, Retry und stabile Message-IDs. | `src/VertexBPMN.Infrastructure/Messaging/RuntimeOutboxPublisherService.cs` | At-least-once-Zustellung ist das Ziel; eine Exactly-once-Garantie darf daraus nicht abgeleitet werden. |
| Die Inbox speichert den Claim vor dem Handler; auch unvollständige vorhandene Claims führen zum Rücksprung und anschließendem Ack. `Payload` referenziert ein bereits freigegebenes `JsonDocument`. | `RuntimeInboxConsumerService.cs`: `ProcessIdempotentlyAsync`, `HandleDeliveryAsync`, `InboxEnvelope.Parse` | Nach Handlerfehler/Absturz droht verlorene Verarbeitung; Payload-Zugriff kann fehlschlagen. Vor Adapterextraktion in P2 beheben. |
| `OidcSessionTokenStore` hält Tokenzustand und Refresh-Locks im Prozess. | `src/VertexBPMN.Studio/Services/OidcSessionTokenStore.cs` | Gemeinsame Data-Protection-Schlüssel lösen weder verteilten Tokenrefresh noch Blazor-Circuit-Failover. |
| Die Dependency Registry verwendet immer SQLite. | `src/VertexBPMN.Infrastructure/InfrastructureModule.cs` | Mehrere ACA-Replikas wären mit einer lokalen Datei nicht sicher betreibbar. |
| API und Studio verlangen in Production/Stage einen dateibasierten Data-Protection-Key-Ring. | `src/VertexBPMN.Infrastructure/InfrastructureModule.cs`, `src/VertexBPMN.Studio/Program.cs` | Ein lokales Container-Dateisystem ist nicht replikaübergreifend persistent. |
| Die vorhandene Deployment-Dokumentation ist Kubernetes-orientiert. | `docs/runbooks/production-deployment.md` | Azure-spezifische IaC-, Identity-, Netzwerk- und Cutover-Artefakte fehlen. |
| Der Aspire AppHost und Compose-Stack sind lokale Entwicklungsprofile. | `src/VertexBPMN.AppHost/AppHost.cs`, `deploy/compose/README.md` | Sie sind keine Produktionsdeployment-Definition. |

## 3. Umsetzungsplan

### P0 – Produktionsentscheidungen und ADRs festschreiben

**Priorität:** Muss  
**Aufwand:** S  
**Abhängigkeiten:** Keine

1. Einen ADR `docs/architecture/adr/` anlegen: Azure Container Apps als Laufzeit, PostgreSQL Flexible Server als Datenhaltung, Service Bus als Messaging und Key Vault als Secret Store.
2. Region, Datenresidenz, Namensschema, Azure-Abonnement, getrennte Resource Groups (`dev`, `stage`, `prod`) und DNS-Verantwortung festlegen.
3. Service-Bus-SKU entscheiden:
   - **Premium** ist Pflicht, falls Private Endpoints/VNet-Isolation, vorhersehbare Kapazität oder strengere Produktions-SLAs gefordert sind.
   - **Standard** ist nur zulässig, wenn die dokumentierten Grenzen, öffentliche Erreichbarkeit und Kapazitätsrisiken akzeptiert sind.
4. Die Semantik der Runtime-Destination verbindlich festlegen:
   - Topic `vertexbpmn-runtime` für Fan-out-Ereignisse; pro fachlichem Konsumenten eine Subscription.
   - Queue nur für konkurrierende Worker-Aufträge.
   - Stabile `MessageId` = Runtime-Outbox-ID; `CorrelationId`, `tenantId`, `eventType`, `processInstanceId` als Application Properties.
   - Duplicate Detection, TTL, maximale Zustellversuche, Lock-Dauer und DLQ-Weiterbehandlung an `LeaseSeconds`, `MaxAttempts` und Geschäfts-SLA ausrichten.
5. In P3 alle tatsächlich verwendeten Outbox-Ereignisse auf Konsumenten abbilden. Zusätzliche Broker-Worker nur bei bestätigtem Bedarf planen; vorhandene HTTP-External-Tasks bleiben unverändert nutzbar.
6. Queue/Topic-Subscriptions vor erstem Publish durch IaC anlegen. Ereignisfilter und konkurrierende Empfänger pro fachlicher Subscription festlegen. Brokerfilter ersetzen keine Tenant-Autorisierung. Reihenfolge pro Prozess ist ohne zusätzlichen Vertrag nicht garantiert; falls erforderlich, Sessions und Sequenzvalidierung einschließlich Publisher-Reihenfolge entwerfen.

**Abnahmekriterium:** ADR ist freigegeben; alle Parameter stehen als nicht-geheime Konfiguration mit klaren Besitzer:innen fest.

#### Umsetzungsstand P0 (2026-09-14)

- ADR `docs/architecture/adr/ADR-0001-Azure-Produktionshosting.md` angelegt und mit Entscheider Yova verbindlich beschlossen: Region **<your-region>**, RG **<RG>** (eine prod-Umgebung, Suffix-Namen), Runtime ACA, PostgreSQL Flexible Server (5 Engine-DBs + Registry), Service Bus **Standard** (nur Outbox/Inbox, keine Remote-Service-Tasks), Key Vault Standard, Blob-LSR-Hot-Key-Ring, Monitor/Log Analytics/App Insights, Entra ID als OIDC, Front Door (Standard) + WAF als öffentlicher Einstieg für Studio **und** API (CLI/SDK/Webhooks), SLOs RPO ≤ 15 min / RTO ≤ 4 h, Domain `studio.<your-domain>` mit DNS-Verbleib bei IONOS (keine Azure-DNS-Zone).
- Budget/SKUs noch **nicht freigegeben**: monatliche Kostenschätzung (Stage/Einstieg vs. Production) in Arbeit (`docs/reviews/2026-09-14_Azure-Kostenschaetzung.md`); **keine kostenpflichtigen Azure-Ressourcen** bis zur Freigabe.

### P1 – Azure-Service-Bus-Adapter für die persistierte Runtime-Outbox

**Priorität:** Muss  
**Aufwand:** L  
**Abhängigkeiten:** P0

1. In `src/VertexBPMN.Infrastructure/VertexBPMN.Infrastructure.csproj` die Azure-SDK-Abhängigkeiten ergänzen: `Azure.Messaging.ServiceBus` und `Azure.Identity`.
2. `RuntimeOutboxOptions` in `src/VertexBPMN.Infrastructure/Messaging/RuntimeOutboxOptions.cs` erweitern, ohne bestehende RabbitMQ-/Kafka-Konfiguration zu brechen:
   - `Provider = AzureServiceBus`
   - `FullyQualifiedNamespace`, `EntityName`, `EntityType` (`Topic` oder `Queue`)
   - `AuthenticationMode` (`ManagedIdentity` oder `ConnectionString` nur für lokale Integrationstests)
   - `TransportType` und begrenzte SDK-Retries; Inbox-Optionen (`Subscription`, `MaxConcurrentCalls`, `PrefetchCount`, Lock-Renewal) separat unter `Runtime:Inbox` definieren.
3. `AzureServiceBusRuntimeOutboxTransport` implementieren, der `IRuntimeOutboxTransport` erfüllt:
   - Langlebigen `ServiceBusClient` und Sender verwenden und sauber entsorgen. Azure verwendet die explizit ausgewählte Managed Identity; lokale Azure-Tests können eine Entwickler-Credential verwenden. Keine unbeabsichtigte Credential-Fallback-Kette in Production.
   - Den existierenden JSON-Envelope unverändert serialisieren.
   - `MessageId` deterministisch aus der Outbox-ID setzen und Korrelation/Tenant/Eventtyp als Properties setzen.
   - SDK-transiente Fehler an den vorhandenen Outbox-Retry zurückgeben; nichttransiente Fehler mit diagnostischem Grund markieren.
   - Health anhand zeitlich begrenzter Betriebs-/Verbindungsindikatoren ausweisen; bloße Client-Erzeugung beweist keine Verbindung. Keine Managementrechte oder zusätzlichen Receive-Rechte nur für Health verlangen. Separate Stage-Sende-/Empfangsprobe beweist End-to-End-Funktion. Broker-Ausfall darf keine Liveness-Neustartschleife auslösen; Readiness-Policy für gepufferte Outbox dokumentieren.
   - SDK-Sendedauer einschließlich Retries gegen Outbox-Lease abgleichen; bei Bedarf Lease-Erneuerung mit Eigentümerprüfung implementieren. Größenlimits inklusive Properties vor Send prüfen, übergroße Nachrichten diagnostizierbar als fehlgeschlagen behandeln.
4. `ConfigureRuntimeOutbox` in `InfrastructureModule.cs` erweitern:
   - `AzureServiceBus` als gültigen Provider in `Production` und `Stage` erlauben.
   - Für Managed Identity keinen Connection String verlangen, wohl aber Namespace und Entity.
   - Für RabbitMQ/Kafka die heutigen Validierungen beibehalten.
   - Fehlertexte provider-spezifisch gestalten, damit Fehlkonfiguration nicht zu stiller Disabled-Outbox führt.
5. Konfigurationsbeispiele in API/Worker-Settings und Runbook ergänzen. Keine echten Namen, Zugangsdaten oder Connection Strings einchecken.

**Abnahmekriterium:** Versand und erneuter Versand verwenden dieselbe ID (`Guid.ToString("N")` wie RabbitMQ). Auch bei verlorener Brokerbestätigung oder Absturz vor DB-Statusupdate geht keine Nachricht verloren. Duplikate sind zulässig; Broker-Deduplizierung ist zeitlich begrenzt und ersetzt die Inbox nicht.

#### Umsetzungsstand P1 (2026-09-17 / gepusht 2026-09-18)

Implementiert, committet und auf `origin/master` gepusht: `84d3b5c` (`feat(infra): Azure Service Bus runtime-outbox transport (P1)`), `3d9d9be` (Konfig-Beispiele) und `eba9818` (Plan-Status).

- Azure-SDK-Pakete `Azure.Messaging.ServiceBus` (7.18.2) + `Azure.Identity` (1.17.1) ergänzt.
- `RuntimeOutboxOptions` erweitert um `FullyQualifiedNamespace`, `EntityName`, `EntityType` (Topic/Queue), `AuthenticationMode` (ManagedIdentity/ConnectionString), `ManagedIdentityClientId`, `OperationTimeoutSeconds` (+ Enum-Typen). Bestehende RabbitMQ-/Kafka-Optionen unverändert.
- `AzureServiceBusRuntimeOutboxTransport` implementiert (`IRuntimeOutboxTransport`, `IAsyncDisposable`): langlebiger Client/Sender mit sauberer Dispose; JSON-Envelope identisch zu RabbitMQ/Kafka; deterministische `MessageId` = Outbox-ID (`N`), Correlation/Tenant/Eventtyp als Application Properties; transiente Fehler → Outbox-Retry, `MessageSizeExceeded` → nichttransient mit diagnostischem Grund; Health = zeitlich begrenzter TCP-Erreichbarkeitstest des Namespace (5671) ohne Managementrechte; Send mit Op-Timeout. Managed-Identity-Credential explizit (optional User-Assigned), keine unbeabsichtigte Fallback-Kette.
- `InfrastructureModule`: `AzureServiceBus` als Production-/Stage-Provider erlaubt; Managed Identity verlangt keinen ConnectionString, wohl aber Namespace+Entity; RabbitMQ-Inbox-Konsument nur noch beim `rabbitmq`-Provider registriert (ASB/Kafka startet keinen RabbitMQ-Consumer; Inbox folgt in P2).
- Konfig-Beleispiele (auskommentiert) in API `appsettings.json` + `appsettings.Stage.json` und env-var-Beispiel + Betriebshinweis im `production-deployment.md`-Runbook; keine echten Namen/Secrets committet.
- **Gate:** Debug Unit-Tests für Guard-/Wiring (5 Tests) grün; CI-äquivalente volle Suite **0 Failed**; Solution-Build 0 Fehler.

**Offen (extern/Stage, nicht ohne Freigabe):** echte End-to-End-Sende-/Empfangsprobe gegen einen realen Service-Bus-Namespace (Stage-Abnahme, benötigt Azure-Budget-/SKU-Freigabe) sowie die License/RG-IaC-Anlage. Inbox-Empfang ist P2.

### P2 – Transportneutrale Runtime-Inbox und Azure-Service-Bus-Empfang

**Priorität:** Muss  
**Aufwand:** L  
**Abhängigkeiten:** P0, P1

1. Zuerst reproduzierende Regressionstests für die Inbox-Lücken schreiben, dann gemeinsame Verarbeitung extrahieren und korrigieren:
   - Payload per `JsonElement.Clone()` vom Dokumentlebenszyklus lösen.
   - Ergebnisse `CompletedDuplicate`, `Completed`, `Busy`, `RetryableFailure` und `Rejected` unterscheiden. Nur abgeschlossene Verarbeitung quittieren; unvollständige Claims müssen nach Fehler/Lease-Ablauf wieder übernehmbar sein.
   - Datenbank-Fachwirkung und Abschlussmarker in einer gemeinsamen Transaktion speichern. Für externe Wirkungen eine weitere Outbox bzw. einen vom Ziel geprüften Idempotenzschlüssel verwenden; keine globale Exactly-once-Zusage.
   - Nur echte Unique-Key-Konflikte als Duplikat behandeln. Fehlende Handler, ungültige IDs, unbekannte Vertragsversionen und unzulässige Tenant-Zuordnung dürfen nicht als erfolgreich verarbeitet gelten.
   - RabbitMQ behält sein Nachrichtenformat; die Fehlerkorrekturen gelten auch dort. Kafka darf keinen RabbitMQ-Consumer starten: fehlenden Kafka-Inbox-Support explizit ausweisen/validieren, ohne den bestehenden Kafka-Outbox-Pfad zu entfernen.
2. `AzureServiceBusRuntimeInboxConsumerService` mit `ServiceBusProcessor` implementieren:
   - Für Topic eine explizit konfigurierte Subscription verlangen; für Queue die Queue verwenden.
   - `PeekLock`, `AutoCompleteMessages=false` und begrenztes Lock-Renewal setzen. Erst nach dauerhaft erfolgreicher Verarbeitung oder nachgewiesen abgeschlossenem Duplikat `Complete` aufrufen.
   - Bei transienten Verarbeitungsfehlern `Abandon`; bei validierungsbedingten, nicht behebbaren Fehlern mit strukturiertem Grund in die DLQ überführen.
   - Sperrverluste, abgebrochene Container und Parallelität korrekt behandeln; keine doppelte Fachwirkung trotz Redelivery.
   - `StartAsync`/`StopAsync` sauber an den Host-Lebenszyklus binden und Fehler mit MessageId, CorrelationId und Entity loggen.
3. Die Registrierung so aufteilen, dass pro aktiver Provider-Konfiguration genau ein Inbox-Konsument startet. Ein Service-Bus-Deployment darf keinen RabbitMQ-Consumer registrieren.
4. Einen operatorfähigen DLQ-Runbook-Abschnitt schreiben: Sichtung, Korrelation zur Outbox/Prozessinstanz, Ursache beheben, kontrolliertes Replay oder Verwerfen, Audit-Nachweis.
5. Replay verwendet eine neue Transport-ID mit ursprünglicher fachlicher Idempotenz-ID und Replay-Auditdaten; andernfalls kann Broker-Duplicate-Detection das Replay verwerfen. Inbox-Schlüssel und Retention müssen diesen Vertrag unterstützen. Zeitfenster, Lock-Renewal und `MaxDeliveryCount` getrennt von Outbox-Lease und `MaxAttempts` konfigurieren.

**Abnahmekriterium:** Tests belegen Wiederaufnahme nach Claim-Absturz, Handlerfehler, Commit-vor-Ack-Absturz und Parallelzustellung. Transaktionale DB-Wirkungen werden nicht doppelt angewandt; externe Wirkungen sind durch den jeweiligen Idempotenzvertrag abgesichert. Poison Messages enden nachvollziehbar in der DLQ.

#### Umsetzungsstand P2 (2026-09-17)

Committet und auf `origin/master` gepusht: `923c4a6` (`feat(infra): P2 transportneutrale Runtime-Inbox + Azure Service Bus Empfang`) und `bb1a729` (Phase-4-Folgeschluss, 8/8 grün). Verifikation unten mit exakten, am 2026-09-18 frisch ausgeführten Ergebnissen.

- **Gemeinsame Verarbeitung extrahiert:** `RuntimeInboxProcessor` (Infrastructure/Messaging) — providerneutral für RabbitMQ- und Azure-Service-Bus-Konsument. Payload wird via `JsonElement.Clone()` vom Quell-`JsonDocument` gelöst (Regression: vorher `ObjectDisposedException`). Ergebnis-Klassifizierung `Completed` / `CompletedDuplicate` / `Busy` / `RetryableFailure` / `Rejected`; nur ein echter Unique-Key-Konflikt gilt als Duplikat. Geschäftswirkung + Abschlussmarker in EINER gemeinsamen Transaktion committet. Fehlender Handler, unparsbares Payload, fehlender EventType, `RuntimeInboxRejectException` (unbekannte Vertragsversion / unzulässige Tenant-Zuordnung) → `Rejected` (nie Fake-Success). `Busy` bei frisch gehaltenem Claim; inkompletter Claim nach konfigurierbarem `Runtime:Inbox:ClaimTimeoutSeconds` wird reclaimt (Wiederaufnahme nach Claim-Absturz). `RuntimeInboxOptions` neu (Enabled, Subscription, MaxConcurrentCalls, PrefetchCount, MaxDeliveryCount, LockRenewalSeconds, ClaimTimeoutSeconds) unter `Runtime:Inbox`.
- **RabbitMQ:** `RuntimeInboxConsumerService` auf den Processor umgestellt; Ack-Semantik Completed/CompletedDuplicate→Ack, Busy/Retryable→Nack(requeue), Rejected→Nack(no-requeue) in DLQ via Dead-Letter-Exchange (`inbox:<dest>.dlq`). Envelope-Parse-Fehler → DLQ.
- **Azure Service Bus:** `AzureServiceBusRuntimeInboxConsumerService` (P2) mit `ServiceBusProcessor`, PeekLock, `AutoCompleteMessages=false`, Lock-Renewal/Prefetch/MaxConcurrentCalls, Topic-Subscription-Pflicht; Completed/CompletedDuplicate→Complete, Busy/Retryable→Abandon (Redelivery, bis Server-`MaxDeliveryCount`→DLQ), Rejected→DeadLetter mit strukturiertem `reason`.
- **Registrierung:** genau EIN Inbox-Konsument pro aktivem Provider; ASB/Kafka-Deployment startet keinen RabbitMQ-Consumer; Kafka (fehlender Inbox-Support) wird beim Aktivieren explizit validiert (Outbox-Publisher unverändert).
- **DLQ-Runbook:** `docs/runbooks/production-deployment.md`, Abschnitt „Dead-Letter-Queue (Inbox, P2)“ — Sichtung/Korrelation, Ursache, kontrolliertes Replay (neue Transport-ID, unveränderte fachliche Idempotenz-ID) oder Verwerfen, Audit.
- **Tests:** `RuntimeInboxProcessorTests` (Cloning, Completed/CompletedDuplicate/Busy/Reclaim, MissingHandler→Rejected, Retryable-kein-Abschluss, RejectException, fehlender EventType/Payload) + Registrierungs-Tests (RabbitMQ-Consumer vorhanden/kein ASB, ASB-Consumer vorhanden/kein RabbitMQ, Kafka wirft explizit). Einheiten grün (SQLite in-memory, echter Unique-Index).
- **Nebenbefund/Regression:** In P1 eingebrachte Kommentar-Beispiele in `appsettings.Stage.json` brachen `P3_AC_06` (strenges `JsonDocument.Parse`); Test nutzt nun `JsonCommentHandling.Skip` (deckt das echte JSONC-Verhalten des Config-Providers ab). Härtungs-Asserts unverändert.
- **Gate (frisch am 2026-09-18 verifiziert):** CI-äquivalente Suite **1139 Tests, 0 Failed, 0 Skipped** (exakt der CI-Command aus `.github/workflows/ci.yml`, `--filter-not-trait`/`--filter-not-class`, Exit 0); `RuntimeInboxProcessorTests` **10/10**, `AzureServiceBusRuntimeOutboxTests` (inkl. 3 neuer Registrierungs-Tests) **8/8**; echte Infra (Postgres 55432 + RabbitMQ 55672) `Phase4OutageAcceptanceTests` **8/8** inkl. `P4_AC_07_Production_Inbox_Consumer_Exactly_Once` gegen den neuen `RuntimeInboxConsumerService`. Solution-Targets (Api/Studio/AgentWorker/Cli) bauen mit 0 Fehler. *Hinweis:* Der volle `VertexBPMN.sln`-Build in dieser Umgebung scheitert nur an fehlenden `project.assets.json` nicht-restorierter Randprojekte (PerformanceRunner/TestRunner/Benchmarks/EntityGenerator/Studio.UiTests) — kein Code-Fehler.

**Offen (extern/Stage, nicht ohne Freigabe):** End-to-End-Sende-/Empfangs-/DLQ-Lauf gegen einen **echten Service-Bus-Namespace** (Stage-Abnahme; benötigt Azure-Budget-/SKU-Freigabe). Der RabbitMQ-seitige Inbox-Konsument ist dagegen lokal gegen echtes RabbitMQ verifiziert (P4_AC_07/Phase4 8/8); ein realer ASB-Empfang bleibt mangels Namespace-Zugang unerprobt.

### P3 – Ereignisverträge und bestehende Worker-Pfade absichern

**Priorität:** Muss; neue Broker-Worker nur bei bestätigtem Bedarf  
**Aufwand:** M  
**Abhängigkeiten:** P0, P1

1. `PersistentMessageDispatcher` und direkte Engine-Outbox-Schreibstellen inventarisieren. Pro Ereignistyp Schema, Tenant-Kontext, Routing, Konsument und fachlichen Abschluss dokumentieren. Insbesondere `ServiceTaskDispatch` und `AiTaskDispatch` auf vollständige Tenant-/Korrelationsdaten prüfen.
2. Den bestehenden Dispatcher beibehalten; Tests beweisen DB-Outbox vor Broker-Versand auch für Service Tasks. Keinen direkten `AzureServiceBusMessageDispatcher` als Ersatz registrieren.
3. Bestehendes Envelope kompatibel versionieren; Worker-Ziel, Tenant und Idempotenzschlüssel validieren. Konsumenten müssen ihre tatsächliche Fachwirkung belegen, nicht nur einen Inbox-Eintrag.
4. HTTP-External-Tasks einschließlich Lease, Completion und Recovery mit `VertexBPMN.AgentWorker` auf Azure testen. Dieses Polling nicht nach Service-Bus-Queue-Länge skalieren.
5. Neue Service-Bus-Worker und Request/Reply sind eine gesonderte Erweiterung, falls fachlich benötigt. Bestehende ausdrücklich nicht unterstützte Dispatcher-Methoden werden durch den Transport nicht implizit implementiert.

**Abnahmekriterium:** Jeder für Azure freigegebene Ereignistyp hat einen dokumentierten und getesteten Verarbeitungsweg; HTTP-External-Tasks funktionieren unverändert.

#### Umsetzungsstand P3 (2026-09-18)

Committet und auf `origin/master` gepusht; Detaildokument: `docs/reviews/2026-09-18_P3_Ereignisvertraege.md`.
- **Inventar:** `81be21d` — Ereignisvertrags-Inventar der Runtime-Outbox (Schema, Tenant, Routing, Konsument/Abschluss) dokumentiert + `P3EventContractTests` (181 Zeilen) für Service/Ai-Task-Durability.
- **Korrelation:** `8129433` — `ProcessInstanceId`/Tenant-Korrelation in `ServiceTaskDispatch`/`AiTaskDispatch` durchgereicht (alle Dispatcher, `PersistentMessageDispatcher`, `DistributedProcessEngine`).
- **Envelope-Konformität (config-gated):** `7e33435` — `TypeSafeEnvelopeConformanceValidator` + `TypeSafeConformanceClient` im Inbox-Pfad (nur wenn `TypeSafe:Enabled`), `TypeSafeConformanceTests` (204) + Acceptance (66).

### P4 – Azure-taugliche Zustands- und Schlüsselverwaltung

**Priorität:** Muss  
**Aufwand:** L  
**Abhängigkeiten:** P0

1. PostgreSQL als zusätzlichen Dependency-Registry-Provider implementieren; SQLite lokal erhalten:
   - `DependencyRegistryDbContext` providerneutral registrieren, analog zu den Engine-DbContexts.
   - PostgreSQL-Migration erzeugen und Upgradepfad von der bestehenden SQLite-Datei bereitstellen.
   - `DependencyConfigurationLoader.LoadInto` und CLI mit umstellen: dort sind `UseSqlite` und automatisches `Database.Migrate()` fest eingebaut. Im Azure-Produktionsprofil liest der Loader nur; Migration läuft ausschließlich über den Migrationspfad. Initiale leere DB darf dessen Start nicht durch vorzeitiges Registry-Lesen blockieren.
   - Gleichzeitige CLI-/API-Zugriffe, Transaktionen und Rollback testen.
   - Bis zur Fertigstellung die API auf genau eine Replika begrenzen; keine Mehrreplika-Freigabe.
2. Data Protection auf einen Azure-tauglichen, gemeinsamen Store umstellen:
   - Key-Ring in dediziertem Blob-Container persistieren.
   - Schlüssel im Key Vault verschlüsseln und Zugriff nur über die Managed Identity erlauben.
   - API und Studio mit unterschiedlichen `ApplicationName`-Werten betreiben, aber jeweils replikaübergreifend denselben Ring verwenden.
   - Dateisystempfad als lokales Entwicklungsfallback erhalten, aber in Azure Production verbieten.
   - Blob-Zugriff und Key-Vault-Wrap/Unwrap-Rollen in IaC ergänzen. API/Studio erhalten getrennte Blob-Objekte; bestehende Key-Rings einschließlich alter Schlüssel kontrolliert übernehmen. Alte Key-Vault-Schlüsselversionen für Restore/Entschlüsselung aufbewahren.
3. Alle fünf Engine-Datenbanken auf PostgreSQL Flexible Server bereitstellen: BPMN, Tenants, Simulation, ProcessMiningEvents und Decision; Verbindungsverschlüsselung und Zertifikatsprüfung erzwingen.
4. Connection Strings, OIDC-Client-Secret, Webhook-/E-Mail-/AI-Schlüssel ausschließlich über Key Vault referenzieren. Nicht-geheime Konfiguration kommt als Container-App-Konfiguration.
5. `OidcSessionTokenStore` für Azure auf einen gemeinsamen verschlüsselten Session-Store (bevorzugt PostgreSQL zur Vermeidung einer weiteren Pflichtressource) umstellen. Refresh pro Session mit verteiltem Lease/Fencing und Versionsprüfung serialisieren; Logout/Revocation und TTL replikaübergreifend umsetzen. Crash nach IdP-Tokenrotation muss zu definiertem Wiederanmelden führen, nicht zur Wiederverwendung veralteter Refresh-Tokens. Lokaler In-Process-Store bleibt verfügbar.
6. Studio verwendet Blazor Interactive Server: WebSockets und Affinität in ACA nachweisen. Ziel ist zunächst Single-Revision-Modus mit Sticky Sessions; ACA-Affinität unterstützt keinen Multi-Revision-Traffic-Split. Circuit-Verlust bei Replikatausch ist möglich: Wiederverbindung, Wiederanmelden und Schutz ungespeicherter Editorarbeit explizit testen. Ein gemeinsamer Token-/Key-Store migriert keine laufenden Circuits.

**Stand P4 (2026-09-18):** Punkt 1 und 5 code-seitig umgesetzt (lokales PostgreSQL), Punkt 2 gegen echte Azure-Ressourcen umgesetzt und verifiziert:
- **Punkt 1:** `DependencyRegistryProvider` providerneutral (SQLite lokal, Npgsql Azure); `DependencyConfigurationLoader.LoadInto` liest ohne Auto-Migration, die CLI ist providerneutral über `IDependencyRegistry`; 4 Postgres-Acceptance-Tests grün (Migration/CRUD/LoadInto-offen/Rollback/parallele Writes) — auch gegen den echten Azure Flexible Server (`<postgres>`, France Central) bestätigt.
- **Punkt 5:** `OidcSessionTokenStore` auf `ISharedOidcSessionStore` umgestellt — lokal `InMemorySharedOidcSessionStore`, Azure `PersistentOidcSessionStore` (verschlüsselt mit DataProtection, monotone Revision für verteiltes Fencing, Logout/Revocation + TTL replikaübergreifend). Providerneutrale Migration (SQLite + Npgsql); lokaler In-Process-Store bleibt Fallback. Postgres- und SQLite-Acceptance/Unit-Tests grün — auch gegen echten Azure Flexible Server (`1/1`).
- **Punkt 2 (2026-09-18):** Data Protection auf Azure Key Ring umgestellt — `DataProtection:Provider=AzureBlobKeyVault` persistiert den Key-Ring in einem dedizierten Blob-Container (`dataprotection-api` bzw. `dataprotection-studio`) und verschlüsselt Schlüssel mit einem Key-Vault-Key (`dataprotection-key`) über `DefaultAzureCredential` (Managed Identity `<dev-mi>`, alle interaktiven Credentials ausgeschlossen). In Production/Stage ist das Dateisystem-Fallback im Azure-Modus verboten; lokal bleibt es Default. API (`VertexBPMN`) und Studio (`VertexBPMN.Studio`) nutzen getrennte ApplicationNames und Blob-Objekte. `AzureDataProtectionKeyRingAcceptanceTests` beweisen Two-Instance-Round-Trip gegen echtes Blob+Key Vault via MI. Commit `39ae2e3`.
- CI-Gate am 2026-09-18: **1179 Tests, 0 Failed** (6 skipped nur wegen fehlender `VERTEXBPMN_TEST_POSTGRES_ADMIN`/`VERTEXBPMN_TEST_AZURE_DP_BLOB_URI` in CI; lokal gegen echte Ressourcen grün).
- **Azure-Ressourcen provisioniert (P4.3/P4.4-Vorbereitung):** Key Vault `<keyvault>` (RBAC), Storage `<blob-storage>`, Flexible Server `<postgres>` (PostgreSQL 16, Basic B1ms, France Central — Deutschland/`<your-region>` ist für diese Subscription für Flexible Server restricted) mit 5 Engine-DBs (`bpmn`, `tenants`, `simulation`, `processminingevents`, `decision`); User-Assigned Managed Identity `<dev-mi>` mit Key-Vault-Secrets-/Crypto-Rollen und Blob-Data-Zugriff; Admin-Passwort ausschließlich als Key-Vault-Secret `pg-admin-password`, nicht im Repo.
- **Offen (Azure-Budget, unverändert):** Punkt 3-Code (Engine-DBs auf Flexible Server als Runtime-Verbindungsstrings + TLS-Erzwingung), Punkt 4 (alle Config-Secrets über Key Vault-Referenzen statt Klartext), Punkt 6 (ACA-WebSockets/Affinität).

**Abnahmekriterium:** Zwei API-/Studio-Replikas bestehen parallelen Login/Refresh, Logout, Restart und Credential-Entschlüsselung. Replikatausch hat einen getesteten UI-Recovery-Pfad; lokale Profile funktionieren ohne Azure-Zugang weiterhin.

### P5 – Infrastruktur als Code und Netzwerk

**Umsetzungsstand P5 (2026-09-18):** **IaC implementiert, committet und gepusht (`ea04c39`)** — aber **noch nicht provisioniert** (echtes Deployment ist kostenpflichtig und wartet auf explizites OK). Umfasst `infra/` mit `main.bicep` + 11 Modulen: Netzwerk (VNet-integrierte ACA-Umgebung, Private Endpoints + Private DNS für PostgreSQL/Key Vault/Service Bus/ACR), Observability (Log Analytics + App Insights), Service Bus (Topic `vertexbpmn-runtime`, Subscriptions api/agent-worker), PostgreSQL (PG16 Flexible Server, 5 Engine-DBs, Firewall), Key Vault (RBAC + `dataprotection-key`), Storage (Data-Protection-Blob-Container), ACR, Container-Apps-Umgebung, Container-Apps (studio extern/Affinität, api intern, agent-worker intern), Migration-Job (`--migrate-only`, 1 Instanz) und Identity/UAMI mit Least-Privilege-RBAC. Connection-Strings der Engine-DBs und weitere Secrets werden als Key-Vault-Referenzen (`secretref:`) verdrahtet, nie als Klartext; Parameterdateien (`stage`/`prod`) enthalten nur nicht-geheime Werte. Verifiziert: `az bicep build` + `what-if` (52 create / 0 Fehler) + CI-Gate 1179/0/6; ein Bicep-Validierungsschritt ist im CI (`ci.yml`) verankert.

**Priorität:** Muss  
**Aufwand:** L  
**Abhängigkeiten:** P0, P4

1. Unter `infra/` eine versionierte Bicep-Struktur anlegen:
   - `main.bicep` pro Umgebung sowie Module für Netzwerk, ACR, Key Vault, PostgreSQL, Service Bus, Container Apps Environment, Container Apps, Migration Job, Observability und RBAC.
   - Parameterdateien enthalten nur nicht-geheime Werte; Secrets werden aus Key Vault bezogen.
2. Netzwerkmodell implementieren:
   - VNet-integrierte Container Apps Environment.
   - Private Endpoints/Private DNS für PostgreSQL, Key Vault, Service Bus und ACR, sofern P0 Premium/Private Networking vorsieht.
   - Pro Dienst das konkrete SKU-/Netzwerkmodell prüfen (ACR Private Link benötigt passenden Premium-Tarif; PostgreSQL-Netzwerkmodell separat wählen). Blob Storage samt DNS und Zugriff einplanen. CI-Runner benötigen für private ACR-/API-Endpunkte explizit einen Netzwerkpfad, z. B. einen kurzlebigen VNet-Runner.
   - Studio über HTTPS öffentlich; API und Agent Worker ohne öffentlichen Ingress.
3. Container Apps erstellen:
   - `vertexbpmn-studio`: extern, Single Revision mit Affinität; zwei Replikas erst nach P4-Session-/Circuit-Abnahme.
   - `vertexbpmn-api`: intern, Readiness-/Liveness-Probes auf die bestehenden Endpunkte.
   - `vertexbpmn-agent-worker`: intern oder ohne Ingress, nur wenn aktiviert.
   - `vertexbpmn-migrate`: manuell gestarteter Container Apps Job mit exakt einer Instanz.
4. User-assigned Managed Identities verwenden und minimal berechtigen:
   - ACR Pull.
   - Key Vault Secrets User sowie Schlüsselverwendung.
   - Service Bus Data Sender für Publisher, Data Receiver für Inbox-/Worker-Apps.
   - Datenbankzugriff über einen dedizierten Anwendungskonto-Mechanismus; Zugangsdaten initial im Key Vault und Rotation dokumentieren.
   - Separate Runtime-DML- und Migration-DDL-Datenbankrollen. Die Migration nutzt den vorhandenen API-Schalter `--migrate-only` mit Timeout, Exit-Code-Prüfung und Serialisierung konkurrierender Releases. Dieser Schalter greift erst nach DI-Aufbau: Startvalidierung und leere Registry gezielt testen, Hosted Services dürfen keine Arbeit ausführen.
5. Skalierung definieren:
   - API nach HTTP-Last und CPU, Obergrenze erst nach Lasttest.
   - Die Inbox läuft derzeit als API-Hosted-Service: optionaler Service-Bus-Scaler skaliert zunächst die API. Separates Inbox-Hosting erfordert einen eigenen Arbeitsschritt. HTTP-AgentWorker nach seinem eigenen Arbeitsvorrat dimensionieren.
   - API mit laufendem Scheduler/Outbox-Polling erhält mindestens eine Replika; HTTP-Scale-to-zero würde neue DB-Outbox-Einträge und Timer nicht wecken. Mehrere Replikas auf Scheduler-/Job-Leases und gemeinsame SignalR-Benachrichtigung prüfen.
   - Service-Bus-Prefetch/Concurrency und Datenbank-Poolgröße gemeinsam begrenzen, damit Backlog-Skalierung die Datenbank nicht überlastet.

**Abnahmekriterium:** Eine leere Umgebung lässt sich vollständig und wiederholbar per Bicep provisionieren; keine Ressource verlangt einen manuell kopierten Secret-Wert.

### P6 – Produktionskonfiguration, Identity und sichere Exposition

**Umsetzungsstand P6 (2026-09-18):** **Offen / nicht begonnen.** Keine Produktionskonfigurationsmatrix, kein ACA-Proxy/OIDC-Härtung, keine öffentliche Exposition umgesetzt.

**Priorität:** Muss  
**Aufwand:** M  
**Abhängigkeiten:** P4, P5

1. Je Umgebung eine vollständige Konfigurationsmatrix dokumentieren: API, Studio, Agent Worker, Migration Job und die zugehörigen Key-Vault-Referenzen.
2. `OperationalMode=Production`, `Database:ApplyMigrationsOnStartup=false`, Runtime Outbox/Inbox und HTTPS-Metadaten in der Azure-Konfiguration erzwingen.
3. Keinen `OidcTest`-, lokalen Development-Login- oder HTTP-Authority-Pfad in Azure bereitstellen. Studio-Redirect-URIs, Logout-URIs, API-Audience und Rollen-/Tenant-Claims für die finale öffentliche Studio-URL registrieren.
4. Keycloak bleibt als austauschbarer OIDC-Provider möglich. Für Azure selbst wird keine Bindung an Entra ID erzwungen; der gewählte Provider muss aber TLS, PKCE, Code Flow, Rollen und Tenant-Claims erfüllen.
5. Reverse-Proxy-Konfiguration an ACA anpassen und mit realen Forwarded Headers testen. Keine harte Liste flüchtiger ACA-Proxy-IP-Adressen einpflegen; die vertrauenswürdige Proxy-Strategie muss für ACA dokumentiert und getestet sein.
6. Eigene Domain mit Zertifikat kann direkt an ACA gebunden werden; Front Door/WAF ist eine separate Schutz-/Verfügbarkeitsentscheidung. Bei Nutzung WebSocket-/Idle-Timeouts und Origin-Absicherung testen. Für CLI, SDK, Webhooks und externe Worker die erforderliche API-Erreichbarkeit ausdrücklich festlegen; interne API ist ein Zielvorschlag, kein bereits bestätigter Funktionsausschluss.
7. Falls Keycloak selbst in Azure betrieben wird, eigene Produktionsbereitstellung mit persistentem Postgres, TLS, Backup, Upgrade und HA-Betriebsverantwortung planen; der lokale Compose-Keycloak genügt dafür nicht.

**Abnahmekriterium:** Ein externer Benutzer kann sich nur über den echten OIDC-Provider anmelden; interne API-Endpunkte und Secrets sind aus dem Internet nicht erreichbar.

### P7 – CI/CD, Migration und revisionssicheres Release

**Umsetzungsstand P7 (2026-09-18):** **Offen / nicht begonnen.** Kein Azure-Release-Workflow, keine OIDC-Federation, kein Migration-Job, kein Rollback-Prozess.

**Priorität:** Muss  
**Aufwand:** L  
**Abhängigkeiten:** P5, P6

1. Einen separaten, schlanken GitHub-Workflow für Azure-Releases anlegen; schnelle PR-Prüfungen bleiben von Azure-Deployment getrennt.
2. GitHub Actions per OIDC Federation an Azure anbinden. Keine langlebigen `AZURE_CLIENT_SECRET`-Werte in GitHub Secrets.
3. Pipeline-Reihenfolge:
   - Vor dem ersten Image-Push einmalig Basisinfrastruktur (ACR, Identitäten, RBAC, Netzwerk und Runner-Zugang) provisionieren. Danach denselben gebauten Digest von Stage nach Production promoten.
   1. deterministischen Build, relevante Unit-/Vertragstests und Image-Scan ausführen;
   2. API, Studio und Worker mit Commit-SHA taggen, nach ACR pushen und Digests erfassen;
   3. Bicep `what-if` für Stage erzeugen, dann nach Environment-Genehmigung anwenden;
   4. Migration Job mit Image-Digest starten und auf Erfolg warten;
   5. API-Kandidatenrevision mit Digest bereitstellen und über internen Kandidatenendpunkt testen; Studio zunächst in Stage mit Single Revision/Affinität abnehmen;
   6. `/api/health/live`, `/api/ready`, Studio-Login und Service-Bus-Smoke-Test aus dem passenden privaten Netz prüfen;
   7. API-Traffic erst nach Abnahme umschalten; Studio-Digest mit Single-Revision-Rollout und geprüftem Reconnect-Verhalten promoten. Kein ungeprüfter Multi-Revision-Split für Studio.
   - Revisionen mit 0 % HTTP-Traffic können bereits Scheduler und Broker-Consumer ausführen. Kandidaten nur mit expliziter Hintergrunddienst-Deaktivierung oder isolierten Test-Entities testen; Aktivierung und Abschaltung alter Consumer revisionsweise koordinieren. Bestehende Flags auf tatsächliche Wirkung für alle Hosted Services prüfen.
4. Für Production ein GitHub-Environment mit manueller Freigabe einsetzen. Jede Freigabe enthält Image-Digest, Infrastruktur-Änderung, Migrationsergebnis und Rollback-Plan.
5. Rollback definieren:
   - API-Rollback über vorherige kompatible Revision; Studio-Rollback über vorherigen Digest im Single-Revision-Modus mit angekündigtem Reconnect. Schema-Readiness muss den Parallelbetrieb alter/neuer kompatibler Schemas erlauben; pauschale Ablehnung aller ausstehenden Migrationen auf Expand/Contract-Kompatibilität prüfen.
   - Datenbankmigrationen müssen vorwärtskompatibel/expand-contract sein; destruktive Änderungen erst nach Ablauf der alten Revision.
   - Vor Broker-Wechsel neue Schreibvorgänge und Scheduler/Publisher kontrolliert anhalten; DB-Outbox einschließlich Failed/Leases, alte Brokerqueues, laufende Zustellungen und Inbox-Claims inventarisieren und drainen/abgleichen. Ein leerer Outbox-Pending-Zähler allein genügt nicht. Nach erstem ASB-Versand erfordert Rückwechsel denselben Abgleich. Kein ungeprüfter Dual-Publish.

**Abnahmekriterium:** Ein Stage-Release und ein kontrollierter Rollback sind ohne Portal-Klicks, mit nachvollziehbaren Digests und auditierbaren Logs reproduzierbar.

### P8 – Tests, Betriebsnachweis und Cutover

**Umsetzungsstand P8 (2026-09-18):** **Offen / nicht begonnen.** Kein Stage-E2E, keine Restore-Übung, kein Cutover.

**Priorität:** Muss  
**Aufwand:** L  
**Abhängigkeiten:** P1 bis P7

1. Automatische Tests ergänzen:
   - Unit-Tests für Provider-Validierung, Nachrichten-Mapping, stabile MessageId, Fehlerklassifikation und Konfigurationsfehler.
   - Integrationstests gegen einen echten, isolierten Service-Bus-Namespace: Senden, Empfang, Duplicate Detection, Restart, Lock-Loss, Retry, DLQ und Replay.
   - Bestehende RabbitMQ- und Kafka-Tests unverändert regressiv ausführen.
   - P1–P4 erhalten ihre Tests unmittelbar bei Implementierung. Lokale Profile explizit abnehmen: Aspire Project, Container, WSLC/ExternalServices, SQLite und lokales PostgreSQL/RabbitMQ; beim lokalen Default darf keine Azure-Credential angefordert werden. Kafka-Outbox plus gültige Inbox-Konfiguration gesondert prüfen.
   - Die echten Azure-Integrationstests als manuell/Stage-gesteuerten Test-Suite markieren; sie gehören nicht in den schnellen GitHub-PR-Workflow und benötigen kurzlebige Test-Credentials oder eine isolierte Test-Managed-Identity.
2. End-to-End-Stage-Test ausführen:
   - Studio-Login, BPMN hochladen/bearbeiten/speichern/deployen/starten.
   - Runtime-Event über Service Bus beobachten und idempotente Verarbeitung beweisen.
   - Prozess mit absichtlich fehlerhafter Integration ausführen und die DLQ-/Incident-Behandlung demonstrieren.
   - Container-Neustart und Skalierung auf mindestens zwei API-/Studio-Replikas testen.
3. Betrieb einrichten:
   - Dashboards für API-Fehlerquote/Latenz, Readiness, Outbox Pending/Alter/Failed, Service-Bus Active/Dead-letter Messages, PostgreSQL-Verbindungen/Speicher und OIDC-Fehler.
   - Alarme für nicht leere DLQ, älteste Pending-Outbox über SLA, fehlgeschlagene Migrationen, Readiness-Ausfälle und PostgreSQL-Speicher/Verbindungsgrenzen.
   - Restore-Übung: fünf Engine-Datenbanken plus Registry, Session-Store, Key-Rings und benötigte Key-Vault-Schlüsselversionen wiederherstellen; gegebenenfalls Keycloak-DB einbeziehen. PostgreSQL-PITR auf einen neuen Server und DNS-/Secret-Umschaltung üben. Brokerzustand ist kein DB-Backup: behaltene Outbox-/Inbox-Daten mit Brokerbestand abgleichen und Replay ohne doppelte Fachwirkung testen. RPO maximal 15 Minuten/RTO maximal 4 Stunden sind zu messende Zielwerte, keine zugesicherte Leistung.
4. Cutover durchführen:
   - Backup/Restore-Probe vor Production.
   - P7-Quiesce-/Drain-Protokoll ausführen, Konfigurationswechsel deployen, Broker-/Outbox-/Inbox-Bestände und fachliche Ergebnisse abgleichen.
   - Für mindestens einen vollständigen Geschäftszyklus beobachten, bevor alte Broker-Ressourcen abgeschaltet werden.

**Abnahmekriterium:** Der dokumentierte Produktionsnachweis enthält erfolgreiche E2E-Szenarien, Skalierung, Fehlerszenario/DLQ, Restore-Übung und eine freigegebene Go-/No-Go-Entscheidung.

## 4. Reihenfolge und kritischer Pfad

1. **P0** Architektur- und Betriebsentscheidungen.
2. **P1/P2** Service-Bus-Outbox und -Inbox samt Vertrags- und Fehlersemantik.
3. **P4** PostgreSQL-Dependency-Registry (Punkt 1 ✅), geteilter OIDC-Session-Store (Punkt 5 ✅) und Azure-Data-Protection-Key-Ring via Blob+Key Vault (Punkt 2 ✅) verifiziert/gepusht; offen: Engine-DBs als Runtime-Verbindungsstrings+TLS (Punkt 3), alle Secrets über Key-Vault-Referenzen (Punkt 4), ACA-WebSockets/Affinität (Punkt 6) – Blocker für Mehrreplika-Freigabe.
4. **P5** Bicep, Netzwerk, Identitäten und Azure-Ressourcen.
5. **P6** Produktionskonfiguration und OIDC/Proxy-Härtung.
6. **P7** revisionssicherer Delivery-Prozess und Migration Job.
7. **P8** Stage-Nachweis, Restore-Probe und kontrollierter Cutover.
8. **P3** Vertragsinventar, Korrelation + TypeSafe-Envelope-Konformität (2026-09-18 ✅); zusätzliche Broker-Worker bleiben bedarfsabhängig. **P5 (IaC) ist der nächste aktive Arbeitsschritt**; Azure-Ressourcen erst nach Budget-/Abonnementfreigabe anlegen.

## 5. Definition of Done

Das Azure-Deployment gilt erst als produktionsbereit, wenn alle folgenden Aussagen belegbar wahr sind:

- API, Studio und erforderliche Worker laufen aus ACR-Image-Digests in Azure Container Apps.
- Im Azure-Mehrreplikaprofil liegt dauerhafter gemeinsamer Zustand außerhalb des Container-Dateisystems. Lokale und selbst gehostete Profile bleiben kompatibel.
- Azure Service Bus transportiert Runtime-Outbox/-Inbox mit getesteter At-least-once-Zustellung und transaktionaler bzw. fachlich abgesicherter Idempotenz. Keine pauschale Exactly-once-Zusage.
- Dead-letter-, Retry-, Alerting- und Replay-Prozess sind getestet und operativ ausführbar.
- Datenbankschemaänderungen laufen ausschließlich im kontrollierten Migration Job.
- OIDC, TLS, interne API-Exposition, Key Vault und Managed-Identity-Rollen sind in Stage und Production geprüft.
- Ein unabhängiger Restore- und Rollback-Test ist dokumentiert erfolgreich.
- Die schnelle GitHub-Prüfung bleibt schnell; nur Azure-Integrationstests und Production-Deployments benötigen ihre explizite, kontrollierte Release-Stufe.

## 6. Offene Entscheidungen vor Implementierungsstart

| Entscheidung | Benötigt von | Blockiert |
|---|---|---|
| Azure Region, Abonnement und Budgetrahmen | Product/Operations | P5 bis P8 |
| Service Bus Premium oder Standard | Product/Operations | P5, Netzwerkmodell |
| Öffentliche Studio-Domain und Front-Door/WAF-Bedarf | Product/Security | P6 |
| Finaler OIDC-Provider und Production-Clients | Product/Security | P6, P8 |
| Service Bus auch für Remote-Service-Tasks? | Product/Architecture | P3 |
| CLI/SDK/Webhook-Zugriff aus externen Netzen | Product/Security | P5, P6 |
| Ziel-SLOs für Latenz, RPO, RTO und Outbox-Backlog | Product/Operations | P0, P8 |

## 7. Primärquellen

- [Azure Container Apps – Overview](https://learn.microsoft.com/en-us/azure/container-apps/overview)
- [Azure Service Bus messaging](https://learn.microsoft.com/en-us/azure/service-bus-messaging/)
- [Azure Service Bus – Well-Architected guidance](https://learn.microsoft.com/en-us/azure/well-architected/service-guides/azure-service-bus)
- [Azure Database for PostgreSQL](https://learn.microsoft.com/en-us/azure/postgresql/overview)
- [Secure access to Azure Key Vault](https://learn.microsoft.com/en-us/azure/key-vault/general/secure-key-vault)
- [Azure Container Registry](https://learn.microsoft.com/en-us/azure/container-registry/)
- [ACA Session Affinity: Single Revision und HTTP-Ingress](https://learn.microsoft.com/en-us/azure/container-apps/sticky-sessions)
- [Service Bus: Locks und Settlement](https://learn.microsoft.com/en-us/azure/service-bus-messaging/message-transfers-locks-settlement)
- [Service Bus: zeitlich begrenzte Duplicate Detection](https://learn.microsoft.com/en-us/azure/service-bus-messaging/duplicate-detection)

## 8. Review-Ergebnis und Nachweisführung

Review vom 17.09.2026: Der ursprüngliche Plan war noch nicht implementierungsreif. Korrigiert wurden der falsch getrennte Dispatcher-/Outbox-Pfad, unbewiesene Exactly-once-Annahmen, Inbox-Recovery, lokaler Providererhalt, OIDC-/Circuit-Skalierung, CLI-Registry-Migration, Health/RBAC und Rollout-Bootstrap. Die Fehlerbefunde beruhen auf Codeinspektion, nicht auf ausgeführten Laufzeittests.

Jede Phase erhält bei Umsetzung Commit/Dateiverweise, konkrete Testbefehle und Ergebnisse sowie separat offene externe Nachweise. `Implementiert`, `lokal getestet` und `in Azure abgenommen` werden getrennt markiert. Kein Punkt ist durch dieses Planreview implementiert oder abgenommen. Bestehende lokale Änderungen anderer Arbeiten gehören nicht zum Azure-Plan.
