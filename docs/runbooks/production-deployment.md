# Produktionsdeployment und Recovery

Dieses Runbook beschreibt das migrationssichere Kubernetes-Rollout der API. Die drei Manifeste sind absichtlich getrennt: Ein API-Pod darf erst erstellt werden, nachdem der einmalige Migrations-Job erfolgreich beendet wurde.

## Voraussetzungen

- Das Container-Image wurde mit einer unveränderlichen Versionsnummer oder einem Digest gebaut. `:latest` ist nicht zulässig.
- PostgreSQL beziehungsweise SQL Server und RabbitMQ beziehungsweise Kafka sind von den Pods erreichbar.
- Die StorageClass unterstützt `ReadWriteMany` für den gemeinsamen Data-Protection-Key-Ring.
- Backup und Wiederherstellung der fünf Engine-Datenbanken wurden für die Zielumgebung getestet.

Das Kubernetes-Secret `vertexbpmn-secrets` muss außerhalb des Repositories erzeugt werden. Es enthält mindestens diese Schlüssel:

```text
ConnectionStrings__Bpmn
ConnectionStrings__Tenants
ConnectionStrings__Simulation
ConnectionStrings__ProcessMiningEvents
ConnectionStrings__Decision
ConnectionStrings__DependencyRegistry
Runtime__Outbox__ConnectionString
Jwt__SecretKey
```

Alternativ zu `Jwt__SecretKey` kann `Jwt__Authority` gesetzt werden. Ein symmetrischer JWT-Schlüssel muss mindestens 32 Byte lang sein. Secret-Werte gehören in einen Secret Manager beziehungsweise in eine verschlüsselte GitOps-Ressource, nicht in ein Klartextmanifest. Der `DependencyRegistry`-Connection-String verweist in der aktuellen Implementierung auf SQLite und muss daher auf das gemeinsame Volume zeigen, zum Beispiel `Data Source=/var/lib/vertexbpmn/dependencies.db`. Für häufige Registry-Schreibzugriffe ist SQLite kein geeigneter verteilter Konfigurationsspeicher; produktive Konfiguration sollte primär über den externen Secret-/Configuration-Store erfolgen.

## Geordnetes Rollout

Die Befehle müssen in dieser Reihenfolge erfolgreich sein:

```bash
kubectl apply -f k8s-prerequisites.yaml
kubectl apply -f k8s-migration-job.yaml
kubectl wait --for=condition=complete job/vertexbpmn-migrate-1-0-0 --timeout=10m
kubectl apply -f k8s-deployment.yaml
kubectl rollout status deployment/vertexbpmn --timeout=10m
```

Vor einer erneuten Ausführung desselben versionierten Jobs muss der bereits abgeschlossene Job gezielt gelöscht oder der Jobname auf die neue Version geändert werden. Das normale API-Deployment setzt `Database__ApplyMigrationsOnStartup=false`: API-Replikas prüfen das Schema beim Start und brechen bei fehlender Verbindung oder ausstehenden Migrationen ab, verändern es aber nicht parallel.

## Betriebsprüfung

- `/api/health/live` prüft ausschließlich, ob der Prozess lebt.
- `/api/ready` prüft alle Engine-Datenbanken auf Verbindung und ausstehende Migrationen sowie die Broker-Verbindung.
- `/api/health` liefert die kombinierte Health-Antwort.
- `/api/metrics/prometheus` stellt persistente Laufzeit-, Job-, Incident-, Worker- und Outbox-Zähler bereit.
- `X-Correlation-ID` wird akzeptiert oder erzeugt und in Antwort, Logs und Traces weitergeführt.

Die Readiness-Probe muss vor dem Umschalten von Traffic erfolgreich sein. Ein fehlgeschlagener Migrations-Job blockiert das Deployment; zuerst dessen Logs und den Datenbankzustand prüfen, nicht die API unter Umgehung des Jobs starten.

## Broker-Ausfall und Wiederanlauf

Runtime-Ereignisse werden dauerhaft in der Datenbank-Outbox gespeichert. Der Publisher least Datensätze atomar, stellt sie mit einer stabilen Message-ID mindestens einmal zu und wiederholt fehlgeschlagene Zustellungen. Konsumenten müssen deshalb anhand der Message-ID idempotent arbeiten. Nach Erreichen von `Runtime__Outbox__MaxAttempts` verbleibt der Datensatz im Zustand `Failed` mit `LastError` zur Diagnose.

Bei einem Broker-Ausfall:

1. `/api/ready`, Broker-Metriken und Outbox-Rückstand prüfen.
2. Broker-Verbindung und Credentials reparieren.
3. Sicherstellen, dass `outbox_pending` fällt und keine neuen permanenten Fehler entstehen.
4. Nachrichten im Zustand `DeadLetter` erst nach Ursachenbehebung kontrolliert auf `Pending` zurücksetzen; Payload und Message-ID dürfen dabei nicht verändert werden.

## Datenbank-Recovery

**Zielwerte (Phase 0, entschieden 2026-09-08):** RPO ≤ 15 min (maximaler Datenverlust), RTO ≤ 4 h (Wiederherstellungszeit).

**Zu sichernde Stores (Konsistentes Backup `P5_AC_01`):**
1. Die fünf Engine-Datenbanken (`Bpmn`, `Tenants`, `Simulation`, `ProcessMiningEvents`, `Decision`) als konsistenter `pg_dump --format=custom --no-owner` je Datenbank.
2. Die Dependency-Registry als Dateikopie (SQLite-Datei, z. B. `dependencies.db`).
3. Der Data-Protection-Key-Ring als Verzeichniskopie (`DataProtection:KeyRingPath`).
4. Broker-/Outbox-Replay: Runtime-Ereignisse liegen dauerhaft in der **Datenbank-Outbox**; nach Restore repliziert der Publisher ausstehende `Pending`-Nachrichten selbst (at-least-once, stabile Message-ID, idempotente Konsumenten via Unique-Index `(TenantScope, Operation, IdempotencyKey)`). Es ist also keine separate Broker-Queue-Sicherung nötig – der konsistente DB-Dump deckt den Replay-Zustand ab.

**Wiederherstellung auf frischer isolierter Umgebung (`P5_AC_02`, gemessene Werte 2026-09-09):**
1. Schreibzugriffe und Publisher stoppen.
2. Alle fünf Engine-Datenbanken sowie die Dependency-Registry und den Data-Protection-Key-Ring aus dem konsistenten Backup wiederherstellen (`pg_restore --no-owner` in eine frische Datenbank).
3. Den versionierten Migrations-Job ausführen und auf erfolgreichen Abschluss warten.
4. Einen einzelnen API-Pod starten und `/api/ready` prüfen.
5. Erst danach auf die gewünschte Replikazahl skalieren und Outbox-Rückstand beobachten.

**Gemessene RPO/RTO (lokal, echte Postgres 17 + RabbitMQ 4, 2026-09-09):** RTO ≈ 3,9 s (Restore 1,2 s + Ready 2,7 s) für eine kleine Bestandsdaten-DB – weit unter dem Ziel von 4 h. RPO = 0 für den konsistenten Dump-Zeitpunkt (Dump wird bei gestoppten Schreibzugriffen erzeugt). Diese Messwerte stammen aus einem lokalen Abnahmelauf (`P5_AC_02`), nicht aus dem Ziel-Cluster; für die endgültige Abnahme in der Zielumgebung sind die Messwerte dort real zu erfassen und gegen Phase 0 (RPO ≤ 15 min, RTO ≤ 4 h) zu belegen. Ein fachlicher Vergleich (überlebende Instanz, offene User-Task, Timer-Jobs) nach Restore ist Pflichtteil.

**Upgrade (`P5_AC_03`):** Bestandsdaten (deployte Modelle, offene Tasks) überleben eine vollständige Migration; der DB-Dump nach `ApplyMigrationsOnStartup=false` verweigert bei ausstehenden Migrationen den Start kontrolliert (kein Serving), ein Migrationsfehler stoppt den Rollout.

**Rollback (`P5_AC_04`):** Kein automatisches Schema-Downgrade. Rückkehr nur bei nachgewiesener Schemakompatibilität; andernfalls den getesteten Backup-Restore verwenden. Eine gegen das erwartete Schema zurückspringende Anwendung darf nicht dienen.

Ein Schema-Downgrade wird nicht automatisch ausgeführt. Für Rollback muss die Anwendungsversion mit dem vorhandenen Schema kompatibel sein oder ein vorab getestetes Restore des Datenbank-Backups erfolgen.

## Last und Kapazität (Phase 6, 2026-09-09)

**Gemessene Betriebsziele (lokal, echte Postgres 17 + RabbitMQ 4, `P6_AC_02`, 660 Ops):**
Gesamt-p95 ≈ 351 ms (< Ziel 1 s), Gesamt-p99 ≈ 574 ms (< Ziel 3 s), Fehlerrate 0; DB-Verbindungen 5→16, Locks 9→12 (kein unbegrenztes Wachstum); Speicher über die Lastphase stabil ≈ 232 MB. Die Rampen-Instant-Tiefpunkte des Outbox-Pending wachsen nur, weil die Burst-Erzeugung kurzzeitig schneller ist als die Drain-Rate des Publishers (50/s je Poll); nach Lastende drainet der Rückstau innerhalb von 90 s auf ≤ 25 (Peak 1150 → final 14) – kein dauerhaft zunehmender Rückstand. **Abnahme-Zielprofil (Phase 0):** 1–10 Starts/s, 50–500 parallele Benutzer. **Kapazitätsprofil:** `docs/reviews/2026-09-09_Phase6_Last_Abnahme.md` (Hardware/Replikazahl, gemessene Sättigung, Grenzen).

- Der API-Outbox-Transport benötigt `Runtime__Outbox__Enabled=true`, `Runtime__Outbox__Provider=RabbitMq` und `Runtime__Outbox__ConnectionString=<AMQP>`; ohne diese Konfiguration fällt der Publisher auf den Disabled-Transport zurück und `Pending`-Nachrichten würden dauerhaft akkumulieren (kein Produktdefekt, aber Betriebsfehler).
- Das globale ASP.NET-Rate-Limit (`RateLimiting:PermitLimit`, Standard 120/60 s je IP) schützt vor Überlast. Für Lasttests muss es (wie in `P6_AC_02`) angehoben werden, damit die Engine-Latenz statt des Limits gemessen wird; in der Produktion ist der Wert als Kapazitätsparameter zu belegen.
- Ein **voller 24–72 h-Dauerlauf** mit Langzeit-Drift/Warmzeit wurde nicht gefahren (kurzer Dauerlauf-Abschnitt mit Lastspitze + kontrollierter Unterbrechung in `P6_AC_03`); er ist nach Zielprofil in der Zielumgebung zu betreiben, bevor eine Kapazitätszusage auf die absoluten Grenzen getroffen wird.
