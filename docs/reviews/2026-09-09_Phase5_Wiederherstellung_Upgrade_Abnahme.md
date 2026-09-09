# Phase 5 – Wiederherstellung und Upgrade (Umsetzungs- und Abnahmeplan)

Stand: 2026-09-09 · Status: **umgesetzt & verifiziert** (5/5 grün gegen echte Infrastruktur)

Dieses Dokument konkretisiert Phase 5 des Produktionsqualitäts-/Release-Abnahmeplans
(Req 5, „Wiederherstellung und Upgrade“). Die Nachweise laufen als Abnahme-Testklasse
`tests/VertexBPMN.Tests/Acceptance/Phase5RecoveryAcceptanceTests.cs` (Kategorie
`Phase5RecoveryAcceptance`, P5_AC_01…05) gegen **echte** PostgreSQL 17 (127.0.0.1:55432)
und RabbitMQ 4 (127.0.0.1:55672).

## Abnahmekriterien → Tests (umgesetzt & verifiziert)

| Kriterium (Plan) | Test | Nachweis |
|---|---|---|
| Konsistente Backups aller Stores inkl. Dependency-Registry + Data-Protection; Broker/Outbox-Replay im Recovery-Konzept | `P5_AC_01_Consistent_Backup_Of_All_Stores` | ✅ `pg_dump --format=custom` der Engine-DB (nicht leer, via `pg_restore --list` validiert), Dateikopie der Dependency-Registry (SQLite) und des Data-Protection-Key-Rings. Broker-/Outbox-Replay im Recovery-Konzept dokumentiert (Outbox dauerhaft in der DB, Publisher repliziert `Pending` nach Restore, idempotente Konsumenten via Unique-Index `(TenantScope, Operation, IdempotencyKey)`). |
| Auf frischer isolierter Umgebung wiederherstellen; Modelle/Decisions/Cases/Tasks/Timer/Instanzen fachlich vergleichen und fortsetzen | `P5_AC_02_Restore_On_Fresh_Isolated_Env_Compare_And_Continue` | ✅ Echte Quelle (User-Task-Prozess + Timer-Prozess, `Job` Type=timer) → konsistenter Dump → Restore in **fremde neue** DB → echte API dagegen → `GET /api/runtime/{id}` überlebt (gleiche id), offene User-Task `GET /api/task/{id}` lebt, Timer-Jobs (`≥` vorher) erhalten → Fortsetzen: `complete -> 204`. RTO gemessen. |
| Upgrade von letzter freigegebener Version mit realistischen Bestandsdaten; Migrationsfehler stoppen Rollout kontrolliert | `P5_AC_03_Upgrade_With_Realistic_Data_Migration_Error_Stops_Rollout` | ✅ Deployte Definitionen + offene Tasks überleben einen vollständigen Re-Migrate (Bestandsdaten erhalten). DB mit nicht-aktuellem Schema (`Database__ApplyMigrationsOnStartup=false`) → `EnsureCurrentAsync` wirft, API-Prozess beendet mit ExitCode≠0 (kontrollierter Stopp, kein Serving). |
| Rückkehr nur bei nachgewiesener Schemakompatibilität; sonst getesteten Backup-Restore | `P5_AC_04_Rollback_Only_With_Schema_Compatibility_Else_Tested_Restore` | ✅ Nicht-kompatibles Schema verweigert den Betrieb (kein stiller Downgrade-Dienst); sanierter Rollback-Pfad = getesteter Backup-Restore (P5-AC-02). Kein automatisches Schema-Downgrade. |
| RPO/RTO messen und Runbook um ausgeführte Schritte + Ergebnisse ergänzen | `P5_AC_05_RPO_RTO_Measured_And_Runbook_Extended` | ✅ Runbook `docs/runbooks/production-deployment.md` (Abschnitt Datenbank-Recovery) um RPO/RTO-Zielwerte, zu sichernde Stores, ausgeführte Restore-Schritte, gemessene RPO/RTO und Upgrade-/Rollback-Verhalten ergänzt; strukturierte Messwert-Doku geprüft. |

## Messwerte (lokal, echte Infra, 2026-09-09)

- **RPO = 0** für den konsistenten Dump-Zeitpunkt (Dump wird bei gestoppten Schreibzugriffen erzeugt).
- **RTO ≈ 3,9 s** für eine kleine Bestandsdaten-DB (Restore 1,2 s + `/api/ready` 2,7 s).
- Zielwerte Phase 0 (RPO ≤ 15 min, RTO ≤ 4 h) lokal klar unterschritten.
- **Grenze:** Messwerte stammen aus dem lokalen Abnahmelauf; für die endgültige Abnahme sind sie in der Zielumgebung real zu erfassen.

## Ausführung

```bash
export VERTEXBPMN_TEST_POSTGRES_ADMIN='Host=127.0.0.1;Port=55432;…;Database=postgres'
export VERTEXBPMN_TEST_RABBITMQ='amqp://…@127.0.0.1:55672/'
./tests/VertexBPMN.Tests/bin/Release/net10.0/VertexBPMN.Tests \
  -method 'VertexBPMN.Tests.Acceptance.Phase5RecoveryAcceptanceTests.*'
```

Ergebnis (verifiziert 2026-09-09): `Total: 5, Errors: 0, Failed: 0, Skipped: 0`.

## Ehrliche Grenzen

- Die Restore-/Wiederanlauf-Szenarien werden auf **Prozess-/Datenbank-Ebene** nachgestellt
  (echte `pg_dump`/`pg_restore`, echter API-Kill + Restart, isolierte DBs), **nicht** als
  echter Kubernetes-Cluster der Zielumgebung — Zielcluster-Abnahme bleibt offen (Phase 0).
- RPO/RTO sind lokale Messwerte, keine Zielumgebungs-Messung; der fachliche Vergleich nach
  Restore (Instanz/Task/Timer/Modelle) ist als Pflichtteil dokumentiert.
- `pg_dump`/`pg_restore` werden via `docker exec vertexbpmn-e2e-pg` aufgerufen (der
  Infrastruktur-Container); die identischen Binärbefehle sind im Zielcluster zu verwenden.
