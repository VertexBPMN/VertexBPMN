# Stage-End-to-End-Abnahme (P8.2)

Gültig für den Stage-Nachweis des Azure/Umsetzungsplans (Phase P8.2). Dieses
Runbook ist der **reviewbare Abnahmeplan** — er beschreibt die Szenarien, die
gegen eine autorisierte Stage-Umgebung ausgeführt werden müssen. Er enthält nur
reale Endpunkte und Metriknamen (aus dem Code verifiziert), keine erfundenen
Probes. Die Ausführung benötigt die laufende Stage-Umgebung (P7) und die
P6-OIDC-Entscheidungen; sie ist ein eigenständiger, manual gesteuerter Schritt —
nicht Teil des schnellen PR-Workflows.

## Voraussetzungen (Stage)

- Stage-Umgebung provisioniert und gesund (P5/P7): Container Apps `api`,
  `studio`, `agent-worker` `Running`, Migrations-Job `Succeeded`.
- Release-Workflow (P7, `.github/workflows/azure-release.yml`) deployt per
  Commit-SHA; Bilder liegen im ACR. Stage-Profile verifiziert (P8.1-lokal:
  RabbitMQ/PG-Transport-Regression grün).
- OIDC-Login am Studio (P6.3) oder, falls noch nicht verdrahtet, der
  Stage-JWT-Weg (`Jwt__SecretKey`/`Jwt__Audience`) für die API-Aufrufe.
- Zugriff auf `/api/health/live`, `/api/ready`, `/api/metrics/prometheus`
  (API intern/VNet-only; Smoke aus dem VNet, siehe P7-Runbook Abschnitt 4).

## Messpunkte (aus dem Code verifiziert)

| Endpunkt | Bedeutung |
| --- | --- |
| `GET /api/health/live` (anonym) | Prozess lebt; registrierte Checks mit Tag `live`. |
| `GET /api/ready` (anonym) | Readiness: nur Checks mit Tag `ready` (DBs, Broker, Migrationen). JSON mit `status`+`results`. |
| `GET /api/metrics/prometheus` | Prometheus-Textformat, Metriken `vertexbpmn_<name>` aus `RuntimeMetricsReader`. |

Relevante Metrik-Namen (aus `RuntimeMetricsReader.cs` / `RuntimeTelemetry.cs`):

| Metrik | Zähler |
| --- | --- |
| `vertexbpmn_process_instances_total` / `_running` / `_completed` | Prozessinstanzen |
| `vertexbpmn_outbox_pending` | DB-Outbox `Pending` |
| `vertexbpmn_outbox_in_flight` | DB-Outbox `InFlight` |
| `vertexbpmn_outbox_dead_letter` | DB-Outbox `DeadLetter` |
| `vertexbpmn_jobs_dead_letter` | Jobs `DeadLetter` (DLQ/Incident) |
| `vertexbpmn_incidents_open` | Offene Incidents |
| OTLP-Meter `VertexBPMN.Runtime.Metrics` | Counter `vertexbpmn.outbox.published` / `vertexbpmn.outbox.failures` |

## Szenario E2E-1: Studio-Login + BPMN-Lebenszyklus

1. Studio über öffentliche URL öffnen, OIDC-Login (oder Stage-JWT).
2. BPMN-Datei hochladen, bearbeiten, speichern.
3. Modell deployen **und** starten.
4. Erfolg prüfen: `process_instances_total` und `_running` steigen; ein
   Runtime-Event entsteht in der **DB-Outbox** (`vertexbpmn_outbox_pending ≥ 1`
   im Intervall) und wird über Service Bus an die Inbox transportiert.

**Abnahme:** Start eines Modells erzeugt +-1 outbox-published-Counter-Inkrement
und die Inbox verarbeitet die Zustellung idempotent (kein doppelter
fachlicher Effekt bei Duplikat-Zustellung).

## Szenario E2E-2: Idempotente Verarbeitung beweisen

1. Ein Runtime-Event mehrfach zustellen (Broker-Retry / Replay mit derselben
   fachlichen Idempotenz-ID, neuer Transport-ID).
2. Nachweis: die fachliche Wirkung (z. B. ein `ProcessMiningEvent` mit
   `SourceEventId`) wird **genau einmal** angelegt; der
   `RuntimeInbox`-Eintrag trägt die Idempotenz-ID und `Result`/`CompletedAt`.

**Abnahme:** Keine doppelte DB-Fachwirkung bei Duplikatzustellung.

## Szenario E2E-3: Fehlerhafte Integration → DLQ/Incident

1. Prozess mit absichtlich fehlerhafter Integration (Service-Task hinter
   unerreichbarem Endpunkt oder ungültiges Envelope) starten.
2. Nachweis: Nachricht landet in der **Dead-Letter-Queue** (ASB: `reason`
   strukturiert; RabbitMQ: `inbox:<dest>.dlq`); `vertexbpmn_outbox_dead_letter`
   bzw. ein DLQ-Zähler steigt; `vertexbpmn_incidents_open` ≥ 1.
3. Ursache beheben, kontrolliertes Replay (neue Transport-ID, unveränderte
   Idempotenz-ID) oder Verwerfen laut DLQ-Runbook
   (`docs/runbooks/production-deployment.md`).

**Abnahme:** Poison Message endet nachvollziehbar in der DLQ; Replay erzeugt
keine doppelte Fachwirkung.

## Szenario E2E-4: Neustart + Skalierung

1. API auf ≥ 2 Replikas bringen, Studio auf ≥ 2 (P4-Entscheidungen beachten:
   Studio-Einzelsession/Affinität bis P4-Punkt 6 abgenommen).
2. Einen Replica-Neustart auslösen (Container-Restart) bei laufender Last.
3. Nachweis: `/api/ready` bleibt gesund, Weiterverarbeitung ohne Verlust
   (DB-Outbox + Inbox leasen atomar; Recovery nach Claim-Absturz).

**Abnahme:** Kein in-flight-Verlust; mind. zwei API- und zwei Studio-Replikas
sind gesund und dienen Traffic.

## Abnahme-Protokoll (Go/No-Go)

Jeden Lauf mit Datum/Uhrzeit, Stage-Revision (Container-App-Revision + Image
Digest), Szenario-Ergebnis (ok/skip/blocked + Grund) und Operator festhalten:

```text
Datum (UTC):        <ISO-8601>
Stage-Revision:     <container-app-revision, image <ACR>/vertexbpmn-api:<SHA>>
Image (API/Studio): <digest>
E2E-1 Login+BPMN:   ok
E2E-2 Idempotenz:   ok
E2E-3 DLQ/Incident: ok
E2E-4 Neustart+Scale: ok
Messung p95/p99 Readiness: <Werte>  (gegen Phase-0-Ziel: p95 < 1s)
Go/No-Go-Entscheidung: <go|no-go> (+ Begründung)
```

Damit ist das P8.2-Abnahmekriterium („dokumentierter Produktionsnachweis mit
erfolgreichen E2E-Szenarien, Skalierung, Fehlerszenario/DLQ“) erfüllbar.
Dashboards und Alarme, die diese Betriebsnachweise belegen, sind in
`infra/modules/monitoring-alerts.bicep` spezifiziert (siehe P8.2 Artefakt 2).
