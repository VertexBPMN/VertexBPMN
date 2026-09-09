# Alerting, Zuständigkeiten und Runbooks (Phase 8)

Dieses Dokument definiert pro Alarm: **Zuständigkeit**, **Auslösebedingung**, **Runbook**, und wie
Prozess-ID, Tenant und Trace ohne Secrets verknüpft werden. Die Signalquellen sind die durable
Engine-Metriken (`/api/metrics`) und der Health-Endpoint (`/api/health`, `/api/ready`); die
Alarm-Schwellen sind operative Richtwerte und vor produktiver Inbetriebnahme mit den
Zielumgebungs-Betreibern zu bestätigen (Phase-0-Profil, Punkt „Backup/Alarmverantwortliche“ offen).

Jede Alarm-Meldung muss tragen: `processInstanceId` (falls vorhanden), `tenantId`, den
**ProcessId/Key**, und eine **Trace-Referenz** (z. B. `traceId`/`correlationId`) – **niemals**
Secrets (keine Passwörter, keine API-Keys, keine Token, keine `X-API-Key`-Kopfzeile). Secrets
werden über den Secret Store/referenzierte Umgebungsvariablen zugeordnet, nicht in Alarmtexten.

| Alarm | Signalquelle | Schwellen-Entwurf | Zuständigkeit | Runbook |
|---|---|---|---|---|
| API-Fehler | Fehlerrate der HTTP-Endpunkte (Log-/Metrik-Aggregation) | Anteil 5xx > X % / Fenster | Plattform-Team / OnCall | `docs/runbooks/incident-response.md`, Abschnitt API |
| Job-/Timer-Lag | `Jobs` (DueDate vs. CompletedAt); Metrik `jobs_dead_letter` | Timer p95-Lag > Zielwert; jeglicher DeadLetter | Engine-Runbook / OnCall | `docs/runbooks/production-deployment.md` § Timer |
| Incidents | `Incidents` (State=Open); Metrik `incidents_open` | > 0 Open über Fenster | Prozess-Linie verantwortlich | `docs/runbooks/incident-response.md` |
| Dead-Letter (Jobs/Outbox) | `jobs_dead_letter`, `outbox_dead_letter` | jeglicher Eintrag | Integrations-/Messaging-Team | Abnahme Phase 4/6 (At-least-once/idempotent) |
| Outbox-Alter | `RuntimeOutbox` (Pending, OccurredAt); Metrik `outbox_pending`, `outbox_dead_letter` | Alter > Zielwert ODER dauerhafter Rückstau | Messaging-Team / OnCall | `docs/runbooks/production-deployment.md` § Outbox |
| Datenbank | `/api/health`, `/api/ready`; `ProcessInstances`/Connections | Health Unhealthy/Degraded | Datenbank-/Plattform-Team | `docs/runbooks/production-deployment.md` § Datenbank-Recovery |

## Signalbezug (Prozess-ID / Tenant / Trace ohne Secrets)

- Jede Metrik und jeder Alarm-Text wird mit `tenantId` und – wo anwendbar – `processInstanceId`
  bzw. `processId` (Key) ausgestattet (durable Metriken in `RuntimeMetricsReader`).
- Trace: OpenTelemetry-Activity (siehe `TelemetryConstants.ActivitySourceName`) + `traceId`/
  `correlationId` in Log-/Alarmzeilen; beides ohne Secret-Werte.
- Geheimnisse werden nur als Referenzen geführt (Secret-Store-Key, Env-Name), nie als Literal.

## Vorgehen bei Auslösung (allgemein)

1. Alarm aus Meldung prüfen: Quellinstanz (Prozess-ID/Key), Tenant, Trace zuordnen; Incident #
   anlegen.
2. Runbook des betroffenen Alarms ausführen (siehe Tabelle).
3. Nach Entwarnung: Ursachenanalyse; betroffene Abnahmetests wiederholen.
4. Schwellen/Verantwortliche im Phase-0-Profil nach Zielumgebungsentscheidung bestätigen.

## Bekannte lokale Nachweise (Phase 8)

- `Phase8MonitoringAcceptanceTests.P8_AC_01_Alert_Signals_Fire_And_Clear_Against_Real_Infra`
  seedet reale Incident-/DeadLetter-Job-/DeadLetter-Outbox-/Pending-Outbox-Signale gegen eine
  echte, isolierte PostgreSQL-Instanz und prüft das **Feuern** (Metriken > 0) und **Entwarnen**
  (Metriken = 0) über `/api/metrics`. DB-abgeleitete Timer-Lag- und Outbox-Alter-Kennwerte werden
  direkt aus der DB geprüft.
- Zielumgebung-/Produktions-Alarmierung (echte Prometheus/Alertmanager-Verantwortliche, echte
  Alarmempfänger) bleibt laut Phase-0-Profil offen bis zur Zielumgebungsentscheidung.
