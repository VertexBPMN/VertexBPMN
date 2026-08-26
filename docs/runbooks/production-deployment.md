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

1. Schreibzugriffe und Publisher stoppen.
2. Alle fünf Engine-Datenbanken sowie die Dependency-Registry aus einem konsistenten Backup wiederherstellen.
3. Den versionierten Migrations-Job ausführen und auf erfolgreichen Abschluss warten.
4. Einen einzelnen API-Pod starten und `/api/ready` prüfen.
5. Erst danach auf die gewünschte Replikazahl skalieren und Outbox-Rückstand beobachten.

Ein Schema-Downgrade wird nicht automatisch ausgeführt. Für Rollback muss die Anwendungsversion mit dem vorhandenen Schema kompatibel sein oder ein vorab getestetes Restore des Datenbank-Backups erfolgen.
