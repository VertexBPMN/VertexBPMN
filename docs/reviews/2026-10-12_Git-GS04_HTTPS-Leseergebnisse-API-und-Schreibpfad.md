# GS04: HTTPS-Leseergebnisse, API und Schreibpfad

Stand: 12. Oktober 2026. Branch `codex/git-generic-servers`, Basis `02ebe35`, uncommittierter lokaler Kandidat. Keine Veröffentlichung, keine produktive Migration, kein Push. Vorhandene Passkeys-Planung und Compilerargumentdateien bleiben außerhalb des Auftrags.

## Implementiert

- Typisierte akzeptierte Lesequeries für Branches, Branchauflösung, Dateiliste, Datei, Historie und Diff. Projektionswerte werden mit Data Protection an Tenant, Actor, Repository, Operation, Requesthash und Fence gebunden gespeichert. Snapshotbytes und Generation bleiben erhalten; erlaubte Ergebnisverträge sind fest begrenzt.
- `ProfiledGitReadService` routet den produktiven Gitprovider und Hintergrundhost zum profilgebundenen HTTPS-Transport. Native Branch-/Historyoperationen verwenden denselben freigegebenen DNS-/CA-Kontext. Read-Worker-Shutdown bleibt wiederaufnehmbar; Caller-Cancellation bleibt getrennt.
- API-Verwaltung von Profilen, CAS-Zuordnung, eigene Repositoryerstellung, typisierte asynchrone Read-Annahme und dauerhafter Ergebniseinstieg. Studio verwendet Profilreferenzen und Repositoryfähigkeiten; ohne Hostingadapter wird keine PR-Aktion angeboten.
- Commit-/Push-Annahme, Claims und Executor sind für HTTPS Basic/PAT auf den bestehenden dauerhaften Jobpfad angeschlossen. Version 2 bindet den kanonischen Request, Profilrevision, Ziel und Netzwerk-/Trustkontext. Native Push-Freigabe ist ausdrücklich erforderlich; transportierte Pushcommands werden nochmals gegen den gespeicherten Auftrag verglichen.
- Bestehender lokaler Commitbeleg, Push-Intent, Ref-CAS und Unknown-Reconciliation bleiben erhalten. Administrative Profilupdates werden am Publish-Transaktionsrand geprüft und gesperrt. Netzwerk-/Rechteprüfungen nach einem möglicherweise wirksamen Push melden bei Änderung `ResultUnknown`.
- Hintergrundhost verarbeitet jeden Auftrag mit eigenem Scope. Definitiv abgewiesene, unveränderte Version-2-Aufträge ohne ersten Claim können als Failed beendet werden; bereits geclaimte oder transient gestörte Aufträge werden dadurch nicht überschrieben.
- Lokale Wiki-/Website-Dokumentation mit Entwicklungs-/Supportgrenzen und [Betriebsrunbook](../runbooks/git-connection-profiles.md). Dokumentationsbranches jeweils `codex/git-generic-servers-docs`.

Wichtige Dateien: `GitReadResultProtection.cs`, `PersistentSourceControlStore.HttpsReadResults.cs`, `.ReadFacade.cs`, `.ProfiledWrites.cs`, `.ProfiledWriteClaims.cs`, `.RejectedDispatch.cs`, `ProfiledGitReadService*.cs`, `ProfiledGitWriteTransport.cs`, `ControlledGitProcess.ApprovedHttps.cs`, `.ApprovedPush.cs`, `SourceControlController.Profiles.cs`, `SourceControlWorkspacePanel.razor*` und `SourceControlJobsHostedService.cs`.

## Tatsächliche lokale Prüfungen

- Release-Gesamtbuild mit `--no-restore -p:SkipBpmnIoAssetBuild=true -m:1 --disable-build-servers`: erfolgreich, keine Warnungen/Fehler. Editorassets unverändert. Erneuter Gesamtbuild nach der abschließenden Dispatch-Korrektur ebenfalls erfolgreich: 1:53,76, keine Warnungen/Fehler.
- Gezielte Profil-/Netzwerk-/Security-/RequiredField-/SQLite-Auswahl: **360 bestanden**, keine Fehler/Skips, 40,039 Sekunden; einschließlich der drei Dispatch-Regressionsfälle.
- Native Auswahl `GitHttpsTransportTests`, `LeaseRunnerTests`, `NativeGitBoundaryTests`, Basic-/IPC-Acceptance und `CommitExecutionAcceptanceTests`: **91 bestanden**, keine Fehler/Skips, 325,862 Sekunden. Neue Fälle belegen SHA-1/SHA-256, Basic-Push, unabhängigen Clone mit exakten Modellbytes, veralteten Expected-Ref-Konflikt und Ablehnung einer reinen Read-Freigabe vor HTTP-Zugriff.
- CI-safe-Auswahl exakt gemäß `.github/workflows/ci.yml`: **1.748 bestanden, acht übersprungen, keine Fehler**, 1.756 insgesamt, 1:48,121. Skips sind vorhandene externe PostgreSQL-/TypeSafe-Voraussetzungen, keine neue Abschwächung. Nach der Dispatch-Korrektur erfolgreich erneut ausgeführt.
- `git diff --check` in Produkt, Wiki und Website erfolgreich. Check-only-Formatprüfung der sechs zuletzt überprüften Transport-/Host-/Dispatchdateien und der neuen Dispatch-/nativen Push-Tests erfolgreich nach manueller LF-/Initializerkorrektur; kein Formatter im Schreibmodus.

Vorherige echte Fehler wurden korrigiert: erforderliche HTTP-Felder zählen jetzt 27 statt 21; Credentials-Negativtests benötigen zum akzeptierten Commit passende Freigaben. Assertions gegen fremde Credentials/Rechte bleiben erhalten. Compiler-/Analyserfehler wurden im Code behoben.

## Scoped Clean-Code-Review

Verwendet: `.agents/skills/dotnet-clean-code-review/SKILL.md`. D1–D7 wurden auf 108 vorhandenen geänderten C#-Dateien ausgeführt; generierte Designer/Snapshotdateien ausgeschlossen. D1–D7 wurden zusätzlich auf dem Schlussprüfungsumfang von neun konkreten Transport-, Rechte-/Host-, Dispatch- und Credential-/Push-Testdateien ohne Suchfehler ausgeführt. Vollständige manuelle aktuelle Schlussprüfung ist auf diese neun Dateien begrenzt. Frühere Paketreviews gelten nur für ihren dokumentierten Stand; kein manueller Gesamt-PASS für alle 108 Dateien.

| Regel | Ergebnis im Schlussprüfungsumfang | Begründung |
| --- | --- | --- |
| Async/Cancellation | geprüft, kein bestätigter Befund | Awaitable I/O, Cancellation weitergereicht, keine neue async-void-Ausführung |
| Collections | geprüft, kein bestätigter Befund | Materialisierte aktuelle Rollen; keine null-Collection eingeführt |
| Bool-Modi | geprüft, kein bestätigter Befund | Remoteoperationen über Enum; Testfälle und Domänenzustände sind Daten |
| Verantwortungsnamen | geprüft, kein bestätigter Befund | Transport, Store und Host haben benannte Zuständigkeiten |
| Regionen | geprüft, kein bestätigter Befund | Keine neuen Regionen |
| Interfaces | geprüft, kein bestätigter Befund | Credential-/Actorports bleiben Sicherheits- und Testgrenzen |
| Vererbung | geprüft, kein bestätigter Befund | Host nutzt Framework-BackgroundService, keine neue eigene Hierarchie |
| Lesbarkeit | geprüft mit Formatkorrekturen | Claim/Dispatch getrennt; Kommentare erklären Unknown-/CAS-Verhalten |

Die Review prüft Wartbarkeit; sie ersetzt keine Security-/Transport-/Produktabnahme. Reviewabdeckung aller angesammelten Änderungen bleibt vor Gesamtqualifikation erneut vollständig durchzuführen.

## Offen

**GS04/Lieferstufe A noch nicht vollständig abgenommen.** Kontrollierte Persistenz-/Lifecycletests und native Testserverprüfungen sind getrennte Nachweise. Die lokale native Fixture injiziert einen ausdrücklich isolierten Peer; die Produktionsnetzwerkgrenze lehnt Loopback weiterhin ab. Noch offen: kombinierter echter API-/Studio-/Server-/Credentiallauf, Prozessverlust/Unknown-Recovery, vollständige Dirty-/Tenant-/Browserregression und Linux.

GS05–GS08 bleiben offen: aktive Hostingregistry/common PR-Pfad, Azure Services/Server mit PAT und Entra separat, sicherer Bearerkanal, SSH-Key-/Hostkeylifecycle, GitLab/Gitea/Forgejo/Enterpriseadapter. Keine externe Infrastruktur oder Rechte wurden bereitgestellt oder verändert. Kein Provider wird wegen generischer HTTPS-Codepfade als qualifiziert ausgewiesen. GS09 teilweise implementiert; GS10-Gesamtqualifikation weiterhin offen.

Prüflogs liegen ignoriert unter `artifacts/git-generic-servers/gs04-profiled-writes-*.log`; keine Credentials in diesem Bericht.
