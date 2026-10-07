# G04 – revisionsgebundenes Git-Objektformat

Basis: `39decbe`, Branch `codex/git-source-control-phase-1`; uncommittierte
Remote-Push-Implementierung erhalten. Keine Migration oder Vertragsänderung.

## Ursache und Korrektur

`GitCommitId` erlaubt vollständige SHA-1- und SHA-256-IDs. Die bisherigen
Arbeitsrepositories wurden dennoch mit dem Git-Standardformat angelegt. Ein
SHA-256-Basiscommit konnte damit nicht zuverlässig gefetcht und verarbeitet
werden; außerdem durfte das Format nicht von administrativer Git-Konfiguration
abhängen.

`ControlledGitProcess.InitializeAsync` wählt das Format jetzt ausschließlich aus
der validierten Basisrevision und übergibt explizit `--object-format=sha1` oder
`--object-format=sha256`. Der bisherige Einstieg ohne Revision setzt ausdrücklich
SHA-1. Commit- und Push-Executor übergeben den unveränderlich angenommenen
Basiscommit. Es gibt weder einen frei übergebenen Git-Parameter noch eine
Konvertierung bereits vorhandener Repositories.

Betroffene Implementierungen:

- `src/VertexBPMN.Infrastructure/SourceControl/ControlledGitProcess.cs`
- `src/VertexBPMN.Infrastructure/SourceControl/SourceControlCommitExecutor.cs`
- `src/VertexBPMN.Infrastructure/SourceControl/SourceControlPushExecutor.cs`

## Lokale Prüfung

Der vorhandene Test
`Https_push_creates_only_workbranch_and_independent_clone_reads_exact_bytes`
wird unverändert hinsichtlich seiner Sicherheitsassertionen für beide Formate
ausgeführt. Reale Repositories, HTTPS mit geprüftem Zertifikat, Auth-Helper und
Git-HTTP-Backend; keine simulierten Push-Ergebnisse. Nachweise: erwartete
OID-Länge, exakte Modellbytes im unabhängigen Clone, unveränderter Defaultbranch,
erlaubter Fast-Forward, Konflikt bei erneutem Create und blockierter Rewind.

Gezielter Lauf: **2 bestanden, 0 Fehler, 0 Skips**, 28,201 Sekunden.
Gesamte lokale Adaptersuite ohne `*PostgresAcceptanceTests`: **116 bestanden,
0 Fehler, 0 Skips**, 3m40,527s. `VERTEXBPMN_TEST_GIT` verweist auf das lokal
installierte Git; Release-Lauf nach aktuellem Build mit `--no-build --no-restore`,
`--max-parallel-test-modules 1 --timeout 10m`. Kein PostgreSQL-Lauf in diesem Paket.
Adapter-Diagnosebuild: **0 Fehler, 8 bestehende Warnungen**.
Hauptprojekt-Diagnosebuild: **0 Fehler, 329 bestehende Warnungen**.
CI-safe Hauptsuite mit unveränderten Ausschlüssen aus `.github/workflows/ci.yml`:
**1.393 bestanden, 0 Fehler, acht bestehende Opt-in-Skips**, 1m31,385s.
Beide Builds verwenden die bereits dokumentierten Diagnoseflags
`RunAnalyzers=false`, `EnforceCodeStyleInBuild=false`,
`TreatWarningsAsErrors=false`; kein erfolgreicher strenger Analyzer-Build behauptet.

Formatprüfung des geänderten Push-Tests: check-only bestanden.
Clean-Code-Skill: D1–D7 und manuelle Prüfung der neuen/geänderten Mitglieder in
fünf C#-Dateien; keine bestätigten neuen Befunde. Insbesondere Cancellation
weitergereicht, keine neuen Boolean-Modusschalter oder öffentlichen Interfaces.

## Grenzen

Der Nachweis betrifft den internen Windows-Git-Adapter. Die reale GitHub-App,
Linux, PostgreSQL, automatische Hintergrundidentität und API-/Studiointegration
sind damit nicht abgenommen. SHA-256-Unterstützung eines Hostinganbieters muss
separat geprüft werden. G04 bleibt als Gesamtpaket offen.
