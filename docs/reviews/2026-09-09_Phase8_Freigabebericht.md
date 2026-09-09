# VertexBPMN – Freigabebericht (Phase 8, Stand 2026-09-09)

Kandidat: `master` @ **`89d3101`** (+ Phase-8-Arbeit, noch nicht committet zum Zeitpunkt der Messung).
Zeitzone Europe/Berlin; Messungen gegen lokale E2E-Infrastruktur (PostgreSQL 17 auf 127.0.0.1:55432,
RabbitMQ 4 auf 127.0.0.1:55672), gleiches Muster wie Phasen 4–7.

## Artepakte und Hashes

| Artefakt | SHA-256 |
|---|---|
| `VertexBPMN.Api.dll` (Release) | `2b55d0011f371731b9d5847eed856a8dc748ec5c9790dc1304a7ff1708d90f9c` |
| `VertexBPMN.Cli.dll` (Release) | `9f8909315496d8b16270dd6d1a050c1e63b080720be328772e760a7ca5ec67c1` |
| `VertexBPMN.Sdk.dll` (Release) | `980917ec99fa499655da3ea05b22223390ca41083a3ffffb1cc7b2e8a0b51291` |
| `VertexBPMN.Cli.1.0.0.nupkg` (dotnet tool) | `538ea8b1e23388fbd711a62defa186b7bec7b23f21305f5d0347861762fe1f41` |
| `VertexBPMN.Sdk.1.0.0.nupkg` (lib) | `a65c6b457cecc5719e6ac561f8bdef20117ca35d7d9fc4e98fe84a903c25c88e` |

Nachweis P8_AC_04 (lokal): Beide Pakete per `dotnet pack -c Release` erzeugt; das CLI-Tool aus dem
`.nupkg` in eine **frische Umgebung** (leerer `--tool-path`, eigener `DOTNET_CLI_HOME`) installiert
und `vertexbpmn --help` mit **Exit 0** und vollständiger Kommandoliste ausgeführt. SDK-Paket als
lib gegen-formatiert (Pack erfolgreich). Installation **auf die Zielumgebung** bleibt ehrlich offen
(Phase-0-Profil: Hosting/OS offen).

## Abnahmetestergebnisse

- **Phase 8 Monitoring-Suite** (`Phase8MonitoringAcceptanceTests.*`): **Total 3, Errors 0, Failed 0,
  Skipped 0** (EXIT=0; 6.67 s).
- Vorgelagerte Phasen (gegen echte Infrastruktur, Stand gemäß früheren Abnahmedokumenten):
  Phase 4 8/8, Phase 5 5/5, Phase 6 4/4, Phase 7 4/4; Phase 2 finaler gebündelter Lauf
  `AcceptancePassed` (1005 Tests, 0 Fehler, 0 Skips); DMN-TCK 3391/3391.

## P8_AC_01 – Alarme/Dashboards: Signalquellen (lokal abgenomen)

`P8_AC_01_Alert_Signals_Fire_And_Clear_Against_Real_Infra` seedet gegen eine echte isolierte
PostgreSQL-Instanz und prüft über `/api/metrics` (live DB-Reader) sowie DB-abgeleitete Kennwerte:

- **Firing:** `incidents_open`, `jobs_dead_letter`, `outbox_dead_letter`, `outbox_pending` je ≥ 1
  nach dem Seeden; DB-abgeleitete `Timer-Lag ≥ 30 min` und `Outbox-Alter ≥ 25 min` nachgewiesen.
- **Clearing (Entwarnen):** nach Auflösen der Incident-/DeadLetter-/Pending-Zeilen alle vier
  Metriken wieder 0.
- `/api/health` bleibt erreichbar (während Signale feuern) — Health ist grün, Incidents sind App-Signal.

## P8_AC_02 – Zuständigkeit + Runbook je Alarm

`docs/runbooks/alerts.md` (neu): je Alarm Signalquelle, Schwellen-Entwurf, Zuständigkeit, Runbook;
Prozess-ID/Tenant/Trace-Verknüpfung ohne Secrets (keine Passwörter/API-Keys in Alarmtexten),
bestätigt durch `P8_AC_02_Alert_Runbook_And_Trace_Correlation_Exist_Secret_Free`.

## Betriebsgrenzen / Restrisiken (ehrlich offen)

1. **P8_AC_03 Pilot:** begrenzter Pilotbetrieb mit realen, vereinbarten Geschäftsabläufen und
   vereinbarter Beobachtungsdauer benötigt die **Zielumgebung** (Phase-0-Profil: Hosting/OS, IdP,
   Backup-/Alarmverantwortliche offen). **Nicht lokal abnahmefähig; offen.** Entscheidungsvorlage:
   [2026-09-09_Phase0_Zielumgebungsentscheidung.md](2026-09-09_Phase0_Zielumgebungsentscheidung.md).
2. **P8_AC_04 Zielumgebungs-Installation:** Artefakte lokal gebaut/verpackt und CLI aus Paket in
   frischer Umgebung getestet; Installation auf die tatsächliche Zielumgebung offen.
3. **Echter IdP / Secret Store / TLS:** Phase-3-Rest; produktive Authentifizierung separat
   abzunehmen.
4. **Subprozess-Boundary-Bewaffnung** (Phase-7-Befund) und **24–72 h-Dauerlauf** in Zielumgebung.
5. **Alarm-Schwellen/Verantwortliche** sind operative Entwürfe; produktiv mit Betreibern zu
   bestätigen.
6. **Rückfallverfahren:** DB-Recovery/Upgrade-Rollback gemäß `docs/runbooks/production-deployment.md`
   (Phase 5 abgenommen, RTO ≈ 3,9 s / RPO 0 lokal); Zielumgebungs-Rückfall offen.

## Freigabe-Fazit

Kein offener **Muss-Punkt des Zielprofils** wird behauptet erfüllt: Die lokal abnahmefähigen
Alarmierungs-Signalquellen, Runbooks, Artefakt-Erzeugung und CLI/SDK-Funktion aus Paketen sind
belegt. Pilot (P8_AC_03) und Zielumgebungs-Installation (P8_AC_04) bleiben bis zur
Phase-0-Zielumgebungsentscheidung offen; der produktive Rollout folgt nach Freigabe des konkreten
Kandidaten auf der Zielumgebung.
