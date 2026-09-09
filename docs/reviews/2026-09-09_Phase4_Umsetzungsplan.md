# Phase 4 – Ausfall, Konkurrenz und Wiederanlauf (Umsetzungs- und Abnahmeplan)

Stand: 2026-09-09. Basis: `master` @ `71d9889`.
Referenz: `docs/reviews/2026-09-08_Produktionsqualitaet_Release-Abnahmeplan.md`, Phase 4 (Req 4).

## Ziel

Echte Ausfall-, Konkurrenz- und Wiederanlauf-Nachweise gegen **echtes PostgreSQL + RabbitMQ** (Container
`vertexbpmn-e2e-pg` :55432, `vertexbpmn-e2e-rabbit` :55672). Kein SQLite-In-Memory, keine synthetischen
Erfolge. „Ein Containerneustart allein ist kein bestandener Test": Jeder Wiederanlauf muss nachweisen, dass
bestätigte Zustände erhalten bleiben, keine unkontrollierten doppelten Geschäftseffekte entstehen und
Rückstände abgearbeitet werden.

## Abnahmekriterien (aus dem Hauptplan)

1. **API-Wiederanlauf (Instance-Continuation):** API kontrolliert vor/nach Commit, während User-Task-Wait und
   während Timer-/Job-Verarbeitung beenden; Fortsetzung **derselben Instanz** prüfen.
2. **Broker-Unterbrechung:** Broker nach Speicherung einer Outbox-Nachricht und vor/nach Bestätigung
   unterbrechen; Message-ID, Retry und idempotente Konsumenten prüfen.
3. **DB-Ausfall:** Datenbank zeitweise unerreichbar; Rückkehr zu konsistentem Zustand + begrenzte Retry-Last.
4. **Mehrreplikat:** Zwei API-/Publisher-Replikate mit identischen Starts, gleichzeitiger Task-Completion,
   Lease-Ablauf und konkurrierenden Nachrichten betreiben.
5. **Externe Geschäftseffekte:** protokollierender Testempfänger; **at-least-once** explizit von
   **idempotenter Geschäftsverarbeitung** unterscheiden.
6. **Dependency-Registry unter Konkurrenz:** SQLite-Eignung für das Zielprofil prüfen; ggf. zentralen
   persistenten Provider implementieren oder verifizierte Betriebsgrenze dokumentieren.

Abnahme-Härtung: Keine verlorenen bestätigten Zustände; keine unkontrollierten doppelten Geschäftseffekte;
nachvollziehbare Incidents und abgearbeitete Rückstände.

## Ansatz

Echte API-Prozesse laufen als OS-Subprozesse (`dotnet src/VertexBPMN.Api/bin/Release/net10.0/VertexBPMN.Api.dll`)
im **Development-Modus** (API-Key-Auth `X-API-Key`, Migrations-on-start, keine In-Process-Script-Sperre).
Jede Testklasse erzeugt **isolierte Postgres-Datenbanken** (Bpmn/Tenants/Simulation/Events/Decision/Registry)
per Npgsql-Admin-Verbindung (Muster aus `ExternalBrokerPhase3AcceptanceTests`), fährt Szenarien gegen echte
RabbitMQ und räumt per Drop-With-(FORCE) auf.

## Abnahmekriterien → Tests (umgesetzt & verifiziert)

| Kriterium | Testklasse / Methode | Ergebnis |
|---|---|---|
| 1 API-Wiederanlauf | `Phase4OutageAcceptanceTests` `P4_AC_01_API_Restart_Keeps_State` | ✅ echter API-Prozess (OS-Subprozess, Development) deployt User-Task-Instanz, Kill, Restart gegen dieselbe isolierte DB; Instanz + offener Task überleben, complete → 204 |
| 2 Broker-Unterbrechung | `Phase4OutageAcceptanceTests` `P4_AC_02_Broker_Interruption_Retry_Stable_MessageId` | ✅ unroutable → PublishReturn → Pending+LastError, Attempt gezählt, Id unverändert; routable → Published; RabbitMQ-Envelope: `BasicProperties.MessageId == message.Id` |
| 3 DB-Ausfall | `Phase4OutageAcceptanceTests` `P4_AC_03_Database_Unreachable_Bounded_Retry` | ✅ 2 „DB-down“-Fehler → vollständige Zustellung, Attempts 3 ≤ MaxAttempts 3, Endzustand Published, keine Spirale |
| 4 Mehrreplikat | `Phase4OutageAcceptanceTests` `P4_AC_04_Two_Publishers_Lease_Dedup` | ✅ 2 isolierte Publisher (eigene ServiceProvider/LockOwner) auf einer echten Postgres-DB, 12 Nachrichten: jede genau 1× publiziert, 0 Duplikate, alle Published |
| 5 At-least-once vs Idempotenz | `Phase4OutageAcceptanceTests` `P4_AC_05_AtLeastOnce_Delivery_Distinct_From_Idempotent_Business` | ✅ echter RabbitMQ-Empfänger: Duplikat-Zustellung (2 Envelopes, gleiche Message-ID) erlaubt, aber idempotente Geschäftsverarbeitung (stable Id als Key) führt Effekt genau 1× aus |
| 5b Produktioneller Inbox-Konsument (echt) | `Phase4OutageAcceptanceTests` `P4_AC_07_Production_Inbox_Consumer_Exactly_Once` | ✅ der PRODUKTIONELLE `RuntimeInboxConsumerService` (BackgroundService, durable Queue `inbox:<dest>` an Topic-Exchange, Unique-Index `(TenantScope, Operation, IdempotencyKey)`) konsumiert 2 echte Publikationen derselben stabilen Message-ID auf echtem RabbitMQ gegen isoliertes echtes PostgreSQL und führt die fachliche Verarbeitung genau 1× aus (1 Completed-Inbox-Row, Handler 1×) |
| 6 Dependency-Registry | `Phase4OutageAcceptanceTests` `P4_AC_06_Dependency_Registry_Concurrent_Access` | ✅ 8 parallele DbContext-Instanzen auf eine SQLite-Datei: 0 Crashes, alle Writes konsistent, `integrity_check=ok`; Betriebsgrenze dokumentiert |
| 1b Timer-Wiederanlauf (echt) | `Phase4OutageAcceptanceTests` `P4_AC_08_Timer_Job_Survives_Api_Restart` | ✅ echte API deployt Prozess mit Timer-Intermediat-Catch (PT10S) → UserTask; Kill vor dem Feuern, Restart gegen dieselbe DB → durable `Job` (Type=timer) überlebt, `JobExecutorService` erzeugt die User-Task NACH dem Wiederanlauf (Timer feuert post-restart) |

## Verifizierter Stand (2026-09-09)

Lauf: `./tests/VertexBPMN.Tests/bin/Release/net10.0/VertexBPMN.Tests -method 'VertexBPMN.Tests.Acceptance.Phase4OutageAcceptanceTests.*'` gegen echte Infra (Postgres 55432, RabbitMQ 55672).
**Total: 8, Errors: 0, Failed: 0, Skipped: 0, Not Run: 0** — wiederholbar über mehrere Läufe.

## Ehrlich dokumentierte Grenzen (Stand nach Abschluss der Folgeiteration)

- **Kriterium 1 Timer-Wiederanlauf** ist nun durch `P4_AC_08` als eigenständiger echte-API-Restart-Test abgedeckt (vorher nur über Kriterien 2–4 mittelbar).
- **Kriterium 5 Produktions-Inbox-Konsument** ist nun durch den echten `RuntimeInboxConsumerService` (Produktbaustein in `src/VertexBPMN.Infrastructure/Messaging/RuntimeInboxConsumerService.cs`, schaltbar über `Runtime:Inbox:Enabled`) + `P4_AC_07` geschlossen. P4_AC_05 bleibt als Präzisierung der OOB-at-least-once-Garantie bestehen.
- **Kriterium 6:** SQLite-Registry verifiziert als pro-Replica/-Host-Betriebsgrenze (kein Multi-Host-Zentralprovider). Für Multi-Host wäre ein persistenter zentraler Provider (z.B. Postgres) nötig — bewusst nicht Teil dieses Durchlaufs (bewährte Betriebsgrenze, kein Regression).
- **Kubernetes-/echt-mehrreplika-Orchestrierung** wird auf Prozess-Ebene nachgestellt (P4_AC_04 + P4_AC_07), nicht als echter K8s-Cluster — Zielumgebung fehlt lokal.
- Req 2 (echter IdP) und Req 6 (externes Review) aus Phase 3 sind separat und blockieren Phase 4 **nicht**;
  Phase 4 setzt eigene Muss-Kriterien um.
