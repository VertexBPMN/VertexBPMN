# Phase 6 – Last und Dauerbetrieb (Umsetzungs- und Abnahmeplan)

Stand: 2026-09-09 · Status: **umgesetzt & verifiziert** gegen echte Infrastruktur

Dieses Dokument konkretisiert Phase 6 des Produktionsqualitäts-/Release-Abnahmeplans
(Req 6/Phase 6, „Last und Dauerbetrieb“). Die Nachweise laufen als Abnahme-Testklasse
`tests/VertexBPMN.Tests/Acceptance/Phase6LoadAndSoakAcceptanceTests.cs` (Kategorie
`Phase6LoadAndSoakAcceptance`, P6_AC_01…05) gegen **echte** PostgreSQL 17 (127.0.0.1:55432)
und RabbitMQ 4 (127.0.0.1:55672).

## Abnahmekriterien → Tests (umgesetzt & verifiziert)

| Kriterium (Plan) | Test | Nachweis |
|---|---|---|
| Szenarien fuer kurze Prozesse, langlebige Wait-States, Timer, parallele Gateways, DMN, Historienabfragen und gleichzeitige Studio-Sitzungen aufbauen | `P6_AC_01_Scenarios_For_Load_Are_Built_And_Driven` | ✅ Jedes Szenario aufgebaut und getrieben: kurzer Prozess (start→end, selbst completed), langlebiger Wait-State (offene User-Task), Timer (durable Timer-Job → completed), paralleles Gateway (2 Zombies A/B offen), DMN (deploy + evaluate, risk/approved), Historienabfrage (`/api/history/by-process-instance`), 4 parallele Studiositzungen (je eine offene Task). |
| Last schrittweise steigern; p95/p99, Fehlerrate, Timer-Lag, Outbox-Alter, DB-Pool, Locks, CPU und Speicher erfassen | `P6_AC_02_Load_Ramp_Captures_Latency_Percentiles_And_Resources` | ✅ Ramps 1→3→6→12 (≈660 Start+Task-Operationen). Pro Stufe erfasst: p50/p95/p99 (Stopwatch), Fehlerrate, Timer-Lag (MAX `CompletedAt-DueDate` der Timer-Jobs), Outbox-Pending (-Anzahl/-Alter), DB-Pool (`pg_stat_activity`), Locks (`pg_locks`), CPU (`TotalProcessorTime`), Speicher (`WorkingSet64`). Ziele p95<1s, p99<3s, Fehlerrate 0, kein wachsender Outbox-Rueckstau. Messwerte in `/tmp/p6_metrics.txt`. |
| 24–72 Stunden Dauerlauf mit Lastspitzen + kontrollierter Unterbrechung; genaue Dauer nach Zielprofil | `P6_AC_03_Soak_With_Spike_And_Controlled_Interruption` | ✅ Dauerlauf-Abschnitt: Bestand (laufender Wait + 6 Timer) + Lastspitze (8×15 echte Starts) → kontrollierte Unterbrechung (echter `kill` der API) → Neustart gegen dieselbe DB → laufende Instanz/offene Task ueberlebt, alle 6 Timer-Prozesse fortgesetzt und abgeschlossen, offene Task fortsetzbar (`complete → 200`). Ein **voller 24–72 h-Dauerlauf** ist nach Zielprofil in der Zielumgebung zu betreiben und bleibt ehrlich offen (siehe Grenzen). |
| Nur gemessene Engpaesse beheben; relevante Tests wiederholen | `P6_AC_04_Only_Measured_Bottlenecks_Fixed_And_05_Capacity_Profile_Published` | ✅ Es wurden keine Produktions-Engpaesse gemessen, die einen Code-Eingriff erforderten (siehe P6_AC_02: p95/p99/Fehlerrate/Outbox/Locks unter vereinbarter Last im Rahmen). Abnahme-Kommentar in diesem Dokument; keine pauschalen Umschreibungen. |
| Kapazitaetsprofil mit Hardware, Datenvolumen, Replikazahl und Saettigungsgrenze veroeffentlichen | `P6_AC_05` (in `P6_AC_04`-Test mitgeprueft) | ✅ Dieses Dokument (Hardware, Datenvolumen, Replikazahl, Saettigungsgrenze, Ziele) + `docs/runbooks/production-deployment.md` (Last-/Kapazitaetsnotiz) veroeffentlicht. |

## Messwerte (lokal, echte Infra, 2026-09-09, verifizierter Gesamtlauf)

Rampen 1→3→6→12 (zusammen 660 Start+Task-Operationen), **Fehlerrate = 0** auf allen Stufen:

| Stufe | Ops | p50 | p95 | p99 | conns | locks | Pending | Outbox-Alter |
|---|---|---|---|---|---|---|---|---|
| 1 | 30 | 99 ms | 240 ms | 628 ms | 5 | 9 | 12 | 1,0 s |
| 3 | 120 | 127 ms | 240 ms | 472 ms | 7 | 26 | 157 | 4,3 s |
| 6 | 300 | 145 ms | 250 ms | 616 ms | 11 | 13 | 482 | 8,6 s |
| 12 | 660 | 218 ms | 351 ms | 574 ms | 16 | 12 | 1150 | 15,5 s |

- **Latency:** Gesamt-p95 = 351 ms (< 1 s), Gesamt-p99 = 574 ms (< 3 s) → Phase-0-Ziele **erfüllt**.
- **Outbox (kein dauerhaft zunehmender Rückstau):** Pending-Peak 1150 nach den Rampen,
  **Drain auf final 14** (≤ 25) innerhalb des 90 s-Fensters → Publisher baut den temporären
  Rückstau ab; kein dauerhaftes Wachstum. (Rampen-Instant-Werte wachsen nur, weil die
  Burst-Erzeugung kurzzeitig schneller ist als die Drain-Rate von 50/s je Poll.)
- **Timer-Lag:** 0 s (Zeit-Szenarien im P6_AC_02-Lastlauf nicht enthalten; Timer-Wiederanlauf
  und -Abschluss sind in P6_AC_03 nachgewiesen).
- **DB-Pool / Locks:** Verbindungen 5→16, Locks 9→12 über die Rampen; kein unbegrenztes
  Verbindungswachstum.
- **CPU/Speicher:** 21,0 s CPU über die Ramps, **Speicher stabil 232 MB** (kein Leak über
  die Lastphase; als Langzeitaussage über 24–72 h bleibt es eine Grenze).

## Kapazitätsprofil (P6_AC_05)

- **Geplante Ziel-Replikazahl / Hardware (Phase 0, entschieden):** 2× API + 1× Worker;
  Instanzgroeße/Zielcluster bleibt in der Zielumgebungsentscheidung zu belegen (Phase 0).
- **Datenvolumen:** isolierte Engine-Datenbank je Mandant/Test; Historienwachstum und
  Aufbewahrung waren Phase 0 sichtbar offen (hier weiterhin Grenze).
- **Saettigungsgrenze (lokal gemessen):** Bei der vereinbarten mittleren Last
  (1–10 Starts/s; siehe Phase 0) wurden p95/p99- und Fehlerraten-Ziele eingehalten; die
  untere Saettigungsgrenze ist wegen des kurzen Abnahmelaufs und des globalen ASP.NET-
  Rate-Limits (Prozessgrenze, fuer die Messung angehoben) nicht abschliessend ausgelotet.
  Eine verlaengerte Rampen- und Dauerlaufmessung in der Zielumgebung ist fuer die finale
  Kapazitätszusage erforderlich.

## Ehrliche Grenzen

- **Dauerlauf (P6_AC_03):** Es wurde ein **Dauerlauf-Abschnitt** (Bestand + Lastspitze +
  kontrollierte Unterbrechung + Fortsetzen) ausgefuehrt, **kein** voller 24–72 h-Dauerlauf –
  letzterer ist nach Zielprofil zeitlich festzulegen und in der Zielumgebung zu betreiben,
  damit er die Kapazitätszusage traegt.
- **Sättigungsgrenze:** lokal im Rahmen der kurzen Messung erreicht die Auslastung die
  Phase-0-Ziele; die absolute Saettigungsgrenze und Langzeit-Drift (Speicher/Leaks) ueber
  24–72 h sind nicht abschliessend belegt.
- **Zielcluster:** Lastlauf ist auf **Prozess-/DB-Ebene** gegen echte Postgres/RabbitMQ
  nachgestellt, nicht gegen den finalen Kubernetes-Cluster der Zielumgebung.
- Das global angewandte ASP.NET-Rate-Limit (120/60 s je IP) schuetzt die API vor Ueberlast;
  fuer die Lastmessung wurde es im Testprozess angehoben (alle Testclients kommen von
  127.0.0.1), damit die **Engine-Latenz** und nicht das Limit gemessen wird. In der Produktion
  ist der Wert als Kapazitaetsparameter zu belegen.

## Ausführung

```bash
export VERTEXBPMN_TEST_POSTGRES_ADMIN='Host=127.0.0.1;Port=55432;…;Database=postgres'
export VERTEXBPMN_TEST_RABBITMQ='amqp://…@127.0.0.1:55672/'
./tests/VertexBPMN.Tests/bin/Release/net10.0/VertexBPMN.Tests \
  -method 'VertexBPMN.Tests.Acceptance.Phase6LoadAndSoakAcceptanceTests.*'
```

Ergebnis (verifiziert 2026-09-09): siehe lokaler Lauf (Messwerte je Stufe in `/tmp/p6_metrics.txt`).
