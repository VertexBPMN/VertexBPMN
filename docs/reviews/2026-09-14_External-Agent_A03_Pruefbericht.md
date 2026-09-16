# External Tasks und Agent-Vertragsprüfung – A03-Prüfbericht

Datum: 2026-09-14
Branch: `codex/external-agent-a00-inventory`
Basiscommit: `9f65335`

## Urteil

Der definierte Umfang von A03 ist implementiert und lokal abgenommen. Claim und Heartbeat verwenden persistente Compare-and-Swap-Übergänge, konkurrierende PostgreSQL-Worker erhalten genau einen Gewinner, und jede Lease besitzt eine neue zufällige ID sowie eine monoton erhöhte Generation. Der separate .NET-Worker begrenzt Parallelität und Polling, hält Leases unabhängig von langer Arbeit am Leben und bricht die Arbeit bei Lease-Verlust oder Shutdown ab.

Dies ist **keine Produktionsfreigabe des External-Task-Features**. Der Worker besitzt absichtlich noch keinen Complete-/Fail-Pfad und keinen konkreten Agent-Handler. Atomare Ergebnisannahme, BPMN-Fortsetzung, Retry-Erschöpfung und Crash-Recovery sind A04. `ExternalTasks:EnableSchedulingPreview` bleibt außerhalb Development/Test fail-closed deaktiviert.

## Implementierter Umfang

- `ExternalTaskLeaseService`: Claim/Heartbeat mit DB-Transaktionen und CAS über Prozessinstanz, Wait-State und Job; abgelaufene Leases können mit neuer Lease-ID und Generation übernommen werden.
- API `/api/external-tasks`: Claim, Heartbeat, Status und paginierte/redigierte Versuchshistorie; unbekannte Requestfelder werden abgewiesen, Requests sind auf 256 KiB und Claimantworten konservativ auf 1 MiB begrenzt.
- Sicherheit: Bearer-only Policy `ExternalTaskWorker`; exakt ein Issuer, Subject und Tenant sowie explizite Topic-/Profilclaims. Tenant, Topic, Profil, Subject und aktuelle Block-/Vertragspolicy werden bei jeder Operation erneut geprüft. Nicht sichtbare Jobs liefern 404.
- Betriebsschutz: eigener identitätsbasierter Token-Bucket mit Burst 5 und dauerhaft 30 Operationen pro Minute; 429 liefert `Retry-After` und einen stabilen Problem-Code.
- `VertexBPMN.AgentWorker`: OAuth2 Client Credentials, Token-Caching, HTTPS beziehungsweise HTTP nur auf Loopback, begrenzte Konfiguration, Parallelität, Backoff/Jitter und Graceful Shutdown.
- Lease-Sicherheit im Worker: Heartbeat läuft parallel zum Handler; Lease-Verlust stoppt den Handler. Die vom Server erneuerte Ablaufzeit wird übernommen. Ungültige oder verlorene HTTP-Antworten bleiben Transportfehler und lösen keinen fachlichen Handlerfehler aus.

## Abnahme E03, E04 und E07

| Fall | Ergebnis | Nachweis |
|---|---|---|
| E03 – zwei gleichzeitige Claims | Bestanden | Zwei unabhängige `BpmnDbContext`-Instanzen und PostgreSQL-Verbindungen werden vor dem Kandidaten-SELECT synchronisiert. Genau ein Worker erhält die Lease. |
| E04 – Ablauf und Übernahme | Für den A03-Umfang bestanden | Abgelaufene Lease wird mit Generation 2 und neuer Lease-ID übernommen; Versuch 1 endet mit `lease_expired`; der alte Worker kann keinen Heartbeat mehr setzen. Complete/Fail werden in A04 ergänzt. |
| E07 – Berechtigungsgrenzen | Bestanden | Fehlende, mehrdeutige und Wildcard-Claims, falscher Worker, fremdes Topic/Profil sowie nachträgliche Policy-Blockierung werden abgewiesen. Reale JWT-Bearer-Handlerfälle prüfen außerdem Admin-only und fehlende Topic-Rechte. |

## Ausgeführte Verifikation

Gezielter Build des Testprojekts und aller A03-Abhängigkeiten:

```powershell
dotnet build tests/VertexBPMN.Tests/VertexBPMN.Tests.csproj --no-restore -v:minimal --disable-build-servers --maxcpucount:1 -p:UseSharedCompilation=false -p:ExternalTaskVerificationBuild=true
```

Ergebnis: **0 Fehler, 24 Warnungen**. Die Warnungen stammen aus bereits vorhandenen, nicht zu A03 gehörenden Tests. `ExternalTaskVerificationBuild=true` umging ausschließlich lokal gesperrte generierte CLI-`obj`-Artefakte; die normale Projekt- und CI-Konfiguration wurde nicht abgeschwächt.

Gezielte A03-Suite mit lokalem PostgreSQL 16:

```powershell
dotnet tests/VertexBPMN.Tests/bin/Debug/net10.0/VertexBPMN.Tests.dll -class '*ExternalTaskPostgresAcceptanceTests' -class '*ExternalTaskApiContractTests' -class '*ExternalTaskLeaseServiceTests' -class '*ExternalTaskWorkerServiceTests' -class '*JwtBearerHandlerOidcTests' -parallelMode none
```

Ergebnis: **37 bestanden, 0 fehlgeschlagen, 0 übersprungen, 0 nicht ausgeführt** in 21,948 s.

Breite lokale External-Task-/Engine-Regression über die in `scripts/test-external-tasks-local.ps1` festgelegten Klassen:

Ergebnis: **230 bestanden, 0 fehlgeschlagen, 0 übersprungen, 0 nicht ausgeführt** in 83,422 s.

## Bewusste Grenzen und nächster Schritt

- Kein Complete-/Fail-Endpunkt und keine Ergebnis-Receipt-Semantik vor A04.
- Kein konkreter KI-/Agent-Handler; der Worker startet bei aktivierter, aber unvollständiger Handlerkonfiguration fail-closed.
- Keine Behauptung einer vollständigen Crash-, Mehrreplikat- oder BPMN-Completion-Abnahme; diese Fälle gehören A04 und A07.
- Der lokale PostgreSQL-Nachweis verwendet echte getrennte Verbindungen, aber keine getrennten OS-Prozesse. Prozess-Crash-Szenarien bleiben A07.

Nächster freigegebener Arbeitsumfang ist **A04 – Completion, Recovery und BPMN-Semantik**.
