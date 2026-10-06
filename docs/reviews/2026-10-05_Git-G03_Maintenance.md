# G03 – Maintenance-Host und sichere Crash-Erkennung

Stand: 2026-10-05; Branch `codex/git-source-control-phase-1`, auf `59c131d` aufbauend. Änderungen noch nicht committed/published. Teilpaket implementiert und lokal geprüft; G02/G03 insgesamt offen.

Folgestand: Der unten historisch dokumentierte HTTPS-Fehler ist inzwischen behoben; TLS/Helper, hostile Metadaten und laufender Abbruch sind lokal bestanden. Typisierte Commit-Annahme ergänzt. Aktuelle Zahlen und Grenzen: [Transport-/Annahmebericht](2026-10-05_Git-G02_Transport-und-G03_Annahme.md).

## Umsetzung

- `src/VertexBPMN.Infrastructure/SourceControl/SourceControlMaintenanceHostedService.cs`: scoped Wartung unmittelbar nach Hoststart und danach einmal pro Minute. Nur bei aktivierter Integration registriert, nicht im normalen Testprofil. Keine Gitinstallation oder Remoteverbindung für die Wartung nötig; deaktivierter Dienst greift weder auf Datenbank noch Workspace zu.
- `PersistentSourceControlStore.DetectExpiredLeasesAsync`: bis zu 100 abgelaufene Running-/Reconciling-Jobs pro Durchlauf, konditional nach Zustand/Fence/Lease. Übergang nach `ResultUnknown`, Fence erhöhen und Lease entfernen. Parallele Erneuerung/neuer Claim gewinnen gegen veraltete Maintenance-Kandidaten. Unklarer Write wird niemals als queued oder erfolgreich behandelt.
- `SourceControlWorkspace.PruneCompletedAsync`: nur kanonisch benannte, nachweisbar zugeordnete und abgelaufene terminale Job-Arbeitsbereiche. Owner-Marker, private Root, Quota-Lock und Linksicherungen des bisherigen Cleanup werden wiederverwendet. `CommittedLocal`, `ResultUnknown`, aktive Jobs und unbekannte Verzeichnisse bleiben erhalten. Read-only-Dateien dürfen ausschließlich im verifizierten eigenen Baum vor dessen Entfernung entsperrt werden.
- Bestehende Snapshot-/Detailretention wird vom Host aufgerufen; dauerhafte Idempotenzreceipts und geschützte Ergebnisse bleiben bestehen. Fehlerlog enthält keine Exception/Verbindung/Dateipfade/Secretpayloads. Nach Fehler erneuter Versuch im folgenden Tick.

## Lokale Nachweise

`tests/VertexBPMN.Tests/Unit/Infrastructure/SourceControlPersistenceTests.cs`: **14 bestanden, 0 fehlgeschlagen, 0 übersprungen**, 8,482 s. Vier neue Fälle:

- `Real_host_startup_detects_crashed_jobs_only_when_integration_is_enabled(true/false)`: tatsächlicher .NET-Host und SQLite, scoped produktive Stores; Startup-Erkennung bzw. kein Zugriff im deaktivierten Modus. Kein separater OS-Prozessneustart behauptet.
- `Maintenance_fences_expired_workers_without_replaying_or_pruning_unknown_effects`: persistenter Status nach neuem DbContext, alter Worker abgelehnt, aktive/queued Jobs erhalten, Reconciliation-Claim mit höherem Fence und unverändertem angenommenem Input.
- `Maintenance_cleans_only_expired_owned_terminal_workspaces_and_preserves_local_commits`: reale private Windows-Verzeichnisse einschließlich read-only-Dateien, Zeitgrenzen, wiederholter Cleanup, Erhaltung lokaler Commits/unbekannter Effekte/fremder Verzeichnisse und aller DB-Receipts.

```powershell
dotnet test tests/VertexBPMN.Tests/VertexBPMN.Tests.csproj --configuration Release -p:SkipBpmnIoAssetBuild=true --filter-class '*SourceControlPersistenceTests' --max-parallel-test-modules 1
```

Solution-Build nach Änderungen erfolgreich: **0 Fehler, 0 Warnungen im inkrementellen Lauf**, 7,17 s; kein warnungsfreier Clean-Build behauptet. Native-Git-Basisprüfung: **1 bestanden**, 0 Fehler/Skips.

Reguläre CI-safe Suite nach aktuellem Build, mit unveränderten Ausschlüssen aus `.github/workflows/ci.yml`: **1.386 gesamt, 1.378 bestanden, 0 fehlgeschlagen, 8 bestehende externe Fälle übersprungen**, 81,712 s. Die vier neuen SQLite-/Hostfälle sind enthalten; externe HTTPS-/PostgreSQL-Abnahme ist daraus nicht abzuleiten.

## Klare Grenzen

- SQL-Server-Abnahme auf ausdrücklichen Nutzerwunsch nicht durchgeführt, kein bestandener Nachweis.
- PostgreSQL-Neustartnachweise aus dem vorherigen Teilpaket nicht erneut ausgeführt; neue Maintenance-Pfade aktuell nur mit SQLite geprüft.
- HTTPS-Git-Gesamttest `GitHttpsTransportTests`: **2 gesamt, 1 bestanden, 1 fehlgeschlagen**, 5,981 s. Unvertrautes Zertifikat wird vor jedem HTTP-Auth-Challenge abgelehnt. Der Positivfall mit lokalem TLS-Git-Backend scheitert mit `ProviderUnavailable`; vorherige Diagnose weist auf Windows-Schannel/Test-CA-Vertrauen hin. TLS-/Revocation-Prüfung wurden nicht abgeschaltet und kein Zertifikat in den Benutzer-Truststore installiert. Ursache/positive Abnahme weiterhin offen.
- Typisierte kanonische Jobannahme sowie Abgleich tatsächlich wirksamer Git-Commit-/Remote-IDs und späterer Deployment-Provenienz bleiben offen. Diese Wartung ist keine genau-einmal-Schreibgarantie und kein vollständiger G04-Provider.
- Keine GitHub-Workflows verändert und keine externen/Native-Git-/Browseranforderungen in die reguläre CI verschoben. Keine vorhandenen Studio-Bundles verändert, keine fremden Repositories/Container bereinigt.
