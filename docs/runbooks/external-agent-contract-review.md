# Runbook: lokaler External Agent für Vertragsprüfung

Stand: 2026-09-15

Dieses Runbook gilt für `agent.contract-review` / `contract-reviewer.v1`. Der Worker verarbeitet
den unveränderlichen Dokument-Snapshot ausschließlich über eine lokale Ollama-Instanz. Es gibt
keinen Cloud-Fallback und kein automatisches Vertragsurteil; jeder erfolgreiche Lauf endet in
der menschlichen Vertragsprüfung.

## Start und Vorprüfung

1. PostgreSQL, API und den konfigurierten OIDC-Provider starten.
2. Ollama starten und `GET http://127.0.0.1:11434/api/version` prüfen.
3. Das freigegebene Modell mit `ollama list` prüfen. Für die A07-Baseline ist dies `qwen3:8b`.
4. Worker-Secret ausschließlich per User Secrets oder Umgebungsvariable bereitstellen.
5. API-Vertrag und Worker auf dasselbe Topic, Profil und dieselbe Profilversion konfigurieren.
6. Worker mit `MaxConcurrency=1`, `MaxTasksPerClaim=1`, Lease 60 s und Heartbeat 20 s starten.
7. In der Betriebssicht prüfen, dass das Profil verfügbar ist und kein wachsender Ready-/Leased-
   Rückstau besteht.

Die vollständige Konfiguration steht im
[Betriebsleitfaden](../guide/external-agent-contract-reviewer.md). Für eine lokale technische
Abnahme:

```powershell
$env:VERTEXBPMN_TEST_POSTGRES_ADMIN = '<lokale Admin-Verbindung ohne Logging des Werts>'
$env:VERTEXBPMN_TEST_OLLAMA_MODEL = 'qwen3:8b'
./scripts/test-external-tasks-local.ps1 -NoBuild -RequireNoSkips
```

## Worker- oder Modell-Ausfall

- Keine Jobs manuell als erfolgreich markieren. Ready-/Leased-Zustand, Versuchshistorie,
  Lease-Ablauf und absolute Deadline in der Betriebssicht prüfen.
- Reagiert Ollama nicht, Worker stoppen, Loopback-Endpunkt und Modell prüfen und anschließend
  den Worker neu starten. Persistierte Jobs werden über Lease-Ablauf und Recovery erneut
  verfügbar; die Deadline wird durch Neustart oder Heartbeat nicht verlängert.
- Bei hoher CPU-Last nur einen Modelljob gleichzeitig zulassen. Kein Parallelitätslimit erhöhen,
  bevor Laufzeit- und Speichermessungen erneut abgenommen wurden.
- Wiederholte `provider_unavailable`, `budget_exhausted` oder Lease-Verluste anhand der
  redigierten Metriken untersuchen. Dokumenttext und Secrets dürfen nicht in Logs kopiert werden.

## Lease-Recovery und Neustart

1. Vor dem Neustart Job-ID, Tenant, Topic, Status, Attempt und Lease-Ablauf notieren; keinen
   Dokumentinhalt exportieren.
2. Worker kontrolliert stoppen. Bei einem Crash bis nach `LockedUntil` warten oder den normalen
   Recovery-Dienst arbeiten lassen.
3. API und Worker neu starten. Prüfen, dass eine neue Lease-Generation entsteht und die alte
   Worker-Instanz mit Heartbeat, Complete und Fail abgewiesen wird.
4. Completion-Receipt und Prozessfortsetzung kontrollieren. Es darf genau eine wirksame
   Fortsetzung geben.

## Incident und Quarantäne

- Endgültige technische Erschöpfung bleibt ein Incident beziehungsweise der modellierte manuelle
  Fehlerpfad; sie darf nicht als fachlich erfolgreicher Review gespeichert werden.
- Bei `result_validation_exhausted` Modell, Digest, Promptversion, Schemaversion und anonymisierte
  Benchmark-ID erfassen. Den Originalvertrag nicht an ein Ticketsystem anhängen.
- Ein Job darf nur über die vorhandenen autorisierten Operatoraktionen behandelt werden. Direkte
  Datenbankänderungen sind kein zulässiger Recovery-Pfad.
- Tenantfremde Sichtbarkeit oder ein akzeptiertes erfundenes Zitat ist ein Security Incident:
  Worker deaktivieren, Credentials rotieren, Auditdaten sichern und keine weiteren Dokumente
  verarbeiten.

## Modellwechsel

1. Neues Modell und Digest lokal installieren, aber das bestehende Profil noch nicht umstellen.
2. Versionierten Fachbenchmark mit unveränderten Dokumenten, Annotationen, Prompt, Schema,
   Parametern und vorab festgelegten Schwellenwerten ausführen.
3. Laufzeit, Speicher, valide Ergebnisse, Kategorie-Präzision/-Recall, kritische Auslassungen und
   Fundstellen-Genauigkeit vergleichen.
4. Freigabe und Rollbackmodell dokumentieren. Erst danach `ContractReviewer:Model` ändern und den
   Worker neu starten.
5. Bei Regression auf den vorherigen Modell-Digest zurückstellen; keine Prompt-/Schemaänderung
   gleichzeitig mit dem Modellwechsel verstecken.

## Aufbewahrung und Datenschutz

- Nur synthetische oder gemäß Betreiberfreigabe anonymisierte Texte verwenden, solange keine
  separate produktive Datenklassifizierungs- und Aufbewahrungsentscheidung vorliegt.
- Modell-Cache, Job-Snapshots, Ergebnisse, Attempts, Auditdaten und Backups entsprechend der
  Betreiber-Policy behandeln. Das Feature selbst definiert keine allgemeine Löschfrist.
- Keine Dokumente, Prompts, Modellantworten oder Secrets in Telemetrie aufnehmen.

## Upgrade

Vor Upgrade von API, Worker, Ollama oder Modell: Datenbanksicherung und Rollbackstand prüfen,
Migrationen anwenden, Konfigurationsvalidator ausführen, E01-E14 sowie Fachbenchmark ausführen
und erst danach Traffic freigeben. Ein grüner Unit-Testlauf ersetzt weder PostgreSQL-/Crash-
Recovery noch die reale Modellabnahme.
