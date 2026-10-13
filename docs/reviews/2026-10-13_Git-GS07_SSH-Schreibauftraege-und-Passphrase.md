# GS07: SSH-Schreibauftraege und Passphrase

Stand: 13. Oktober 2026. Branch `codex/git-generic-servers`, auf Basis `25ca93ece6872852a3b8fd6bd794b84fe5951f3a`.

## Implementierung

- Eigener geschuetzter SSH-Write-Vertrag Version 5; Read-Version 4 und HTTPS-Version 2 bleiben kompatibel. Der transportneutrale interne Write-Kontext erhaelt die bisherigen HTTPS-Fingerprints exakt.
- Annahme und Claiming von Commit/Push pruefen Actor, Tenant, Grants, Profilrevision, Requestdigest und exakte administrative Netzwerk-/Hostkey-Snapshots. Policyaenderung oder Widerruf retargetiert keine alten Auftraege.
- Native SSH-Schreibfunktionen verwenden DNS-freigegebene feste IP, pinned Hostkey, private Authentifizierungsdateien und frische Credentiallease. Commit laedt den festen Basiscommit; Push prueft Objekt, Fast-Forward und erwartete Ref und aktualisiert ausschliesslich den eigenen Arbeitsbranch.
- Bestehende Executor-/Runnerkette fuer Write-Intent, Claimheartbeat, Fence, lokale Commitrekonstruktion und Remote-Ref-Abgleich wird wiederverwendet. Ungewisse Writes werden nicht blind wiederholt. Worker verarbeitet Version 5; Studio-/API-Faehigkeiten beruecksichtigen implementierte SSH-Commit-/Push-Funktionen und aktuelle Grants.
- Auditexport entschluesselt auch bestaetigte Version-5-Schreibbelege mit der urspruenglichen Profil-/Credentialreferenz. Keine Secrets im Audit.
- Optionales `GitSshKey`-Secretfeld `passphrase`: getrennte Aufloesung, Rotation und Rechte erneut geprueft, begrenzte Lease, Speicher beim Dispose geloescht. AuthHelper bekommt nur einen zufaelligen Kanalnamen und den lokalen Prompt; Passwort-/fremde Schluesselprompts geben keine Passphrase frei. Einmalige Pipe nur fuer denselben Serviceaccount, kein Agent oder Secret in Dateien/Argumenten/Environment.

Relevante Dateien: `AcceptedGitWriteExecution.cs`, `AcceptedGitSshWriteExecution.cs`, `GitSshWriteExecutionProtection.cs`, `PersistentSourceControlStore.ProfiledWrites.cs`, `.ProfiledWriteClaims.cs`, `.RepositoryAvailability.cs`, `.Audit.cs`, `ProfiledGitWriteTransport.Ssh.cs`, `ControlledGitProcess.SshPush.cs`, `GitSshPassphraseChannel.cs`, `GitSshCredentialLease.cs`, `ProfiledSshCredentialBroker.cs`, `VertexBPMN.SourceControl.AuthHelper/SshPassphraseClient.cs`.

## Pruefung

Finaler vollstaendiger Release-Build: 0 Warnungen/Fehler, 1:36.33 (`artifacts/git-generic-servers/ssh-writes-final-build.log`). Editorassets unveraendert, daher `SkipBpmnIoAssetBuild=true`. Gezielte Haupttests: 356 bestanden, 0 Fehler/Skips, 70.225 s (`ssh-writes-targeted.log`). Native Helper-/GitHub-Transportpruefung: 72 bestanden, 0 Fehler/Skips, 13.969 s (`ssh-writes-native-helper.log`). Die erste gezielte Runde hatte einen falschen erwarteten Fehlercode fuer verlorenes Read-Recht: Die vorhandene RepositoryAccessPolicy verbirgt das Repository als NotFound. Nur die neue Erwartung wurde korrigiert; Autorisierung blieb unveraendert. Neue Regressionen pruefen Annahme/Restart/Fence, Policy-/Rechte-/Belegmanipulation, unveraenderliche Pushquelle/Intent und Credentialrotation. Ein eigener Test mit dem echten paketierten AuthHelper prueft erlaubte und abgewiesene Passphraseprompts; das ist kein SSH-Serverlauf.

CI-safe-Suite mit unveraenderter Ausschlussliste aus `.github/workflows/ci.yml`: 2007 insgesamt, 1999 bestanden, 8 bestehende externe Skips, 0 Fehler; 2:33.269 (`ssh-writes-ci.log`). Die Skips sind keine durchgefuehrten externen Abnahmen. Keine Cloud-/Produktionsressourcen oder Betriebsdatenbanken veraendert.

## Grenzen und offene Abnahmen

- Kein SSH-Server vorhanden, weder Windows noch Debian-WSL; keine fehlenden Tools installiert. Echte SSH-Fetch-/Push-/Passphrase-, Hostkeywechsel-, Betriebssystem- und Prozessverlustabnahme bleibt nicht durchgefuehrt.
- Browser/API-Gesamtworkflow und Multi-Replica-/Storageausfaelle muessen im vorgesehenen Testbetrieb abgenommen werden. Lokale Persistenz-/Callbacktests ersetzen dies nicht.
- Azure-Services-Adapter, PAT/Entra-Lifecycle und Auditmigrationen auf echten PostgreSQL-/SQL-Server-Instanzen bleiben separat extern abzunehmen. Azure-Hosting ist aktuell an die dokumentierte HTTPS-Repoform gebunden; daraus folgt keine Azure-SSH-PR-Unterstuetzung.
- Weitere Hostingadapter, insbesondere GitLab/Gitea/Forgejo/Enterprise, sind nicht durch generischen Git-Transport implementiert. Ohne Adapter nur Git-Grundfunktionen. Der gesamte GS10-Qualifikationsumfang bleibt offen.
- Dokumentation und Wiki/Website auf Entwicklungsbranches sind keine Veroeffentlichung oder Produktionsfreigabe.

OpenSSH-Grundlagen: [ssh(1), SSH_ASKPASS_REQUIRE](https://man.openbsd.org/ssh.1), [native Schluesselpassphrase und BatchMode](https://github.com/openssh/openssh-portable/blob/master/sshconnect2.c). BatchMode wird nur beim expliziten Passphrasecredential umgestellt; Passwort/Keyboard-Interactive bleiben aus und NumberOfPasswordPrompts ist auf eins begrenzt.

## Ausgefuehrte Befehle

```powershell
dotnet build VertexBPMN.sln --configuration Release --no-restore -p:SkipBpmnIoAssetBuild=true -m:1 --disable-build-servers
dotnet tests/VertexBPMN.Tests/bin/Release/net10.0/VertexBPMN.Tests.dll -class '*GitSshAuthenticationRecoveryTests' -class '*GitSshBoundaryTests' -class '*SourceControlConnectionBindingTests' -class '*SourceControlPersistenceTests' -class '*SourceControlOptionsTests' -class '*AzureDevOpsHostingTests' -class '*SourceControlAuditMigrationSqlTests'
$env:VERTEXBPMN_TEST_GIT = (Get-Command git).Source
dotnet tests/VertexBPMN.SourceControl.Tests/bin/Release/net10.0/VertexBPMN.SourceControl.Tests.dll -class '*SshPassphraseChannelTests' -class '*BasicCredentialChannelTests' -class '*CredentialChannelAcceptanceTests' -class '*GitHubPullRequestTransportTests' -class '*GitHubPullRequestResponseTests' -class '*GitHubPullRequestReconciliationTests'
dotnet test tests/VertexBPMN.Tests/VertexBPMN.Tests.csproj --configuration Release --no-build --no-restore --filter-not-trait 'Category=Phase3ExternalAcceptance' --filter-not-trait 'Category=Phase4OutageAcceptance' --filter-not-trait 'Category=Phase5RecoveryAcceptance' --filter-not-trait 'Category=Phase6LoadAndSoakAcceptance' --filter-not-trait 'Category=Phase7ConformanceAcceptance' --filter-not-trait 'Category=Phase8MonitoringAcceptance' --filter-not-trait 'Category=ExternalTaskPostgres' --filter-not-trait 'Category=ExternalAgentProcessRecovery' --filter-not-trait 'Category=ContractReviewOllama' --filter-not-trait 'Category=ContractReviewBenchmark' --filter-not-class 'VertexBPMN.Tests.Conformance.DistributedConformanceTests' --max-parallel-test-modules 1
```

Formatierung: `dotnet format whitespace <projekt> --no-restore --verify-no-changes --include <eigene Dateien>` auf Infrastructure, Haupttests, AuthHelper und SourceControl.Tests. Exakter Dateiscope und Ausgaben lokal in `artifacts/git-generic-servers/ssh-writes-owned-files.json` beziehungsweise `ssh-writes-format-*.log`. D1-D7-Protokoll: `ssh-writes-clean-code.log`. HTML von `plugins.html` geparst, aktueller Berichtslink/Stand geprueft, `git diff --check` in Produkt/Wiki/Website ausgefuehrt; drei fremde Datei-SHA256 unveraendert. Keine Browserabnahme.

## Abschliessendes C#-Review

Scope: 27 eigene C#-Dateien; neue Dateien vollstaendig und bei bestehenden Dateien die geaenderten Mitglieder/Callsites. Basis `25ca93ece6872852a3b8fd6bd794b84fe5951f3a`. D1-D7 ausgefuehrt, Readability manuell geprueft. Keine bestaetigten verbleibenden Clean-Code-Befunde; Sicherheits-/Feature-/Serverqualifikation ist davon getrennt.

| Regel | Ergebnis | Nachweis |
| --- | --- | --- |
| Async/Cancellation | PASS | Native Arbeit, Pipeclient/server, DNS, Credentials und Store reichen Tokens weiter; Dispose wartet auf Channeltask. |
| Collectionvertraege | PASS | Begrenzte Konfigurationen und Commands als Readonly-Listen; Rollen als Collections; keine neue unkontrollierte Lazy-I/O-Enumeration. |
| Boolean-Modi | PASS | HasPassphrase ist Credentialzustand; nativer Fehlerclassifier ist explizite Funktion; keine neuen unklaren bool-Parameter. |
| Verantwortungsnamen | PASS | Getrennte Writevertraege, Schutz, Transport, Passphrasekanal und Client. |
| Regions/Struktur | PASS | Keine neuen Regions; bestehende kohesive Partialstruktur erhalten. |
| Interfaces/Grenzen | PASS | Bestehende Actor-/Credential-Ports erhalten; keine neue unnoetige Interface; optionale SSH-Abhaengigkeit erhaelt interne bestehende Konstruktoren und schlaegt ohne Backend geschlossen fehl. |
| Vererbung | PASS | Keine neue eigene Klassenhierarchie; Framework-/Lifetimevertraege und bestehende Partialtypen. |
| Lesbarkeit | PASS | Transportzweige explizit; HTTPS-Fingerprintkompatibilitaet nachvollziehbar; lokaler Prompt genau an OpenSSH gebunden. |

Whitespace-Check-only fuer 24 Dateien auf vier Projekten. In `PersistentSourceControlStore.cs`, `PersistentSourceControlStore.RepositoryAvailability.cs` und AuthHelper `Program.cs` nur geaenderte Mitglieder manuell geprueft; deren vorhandene Formatierung wurde nicht breit umgeschrieben. Keine Formatterausfuehrung im Schreibmodus. Urspruenglich diagnostizierte eigene Whitespaceaenderungen exakt korrigiert und Checks erneut ausgefuehrt.
