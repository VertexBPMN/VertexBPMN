# Git GS02: Profilzuordnung und Worker-Vertragsgrenze

Stand: 11. Oktober 2026. Branch `codex/git-generic-servers`, Basis `02ebe35`. Fortsetzung der [Profilpersistenz](2026-10-11_Git-GS02_Profilpersistenz-und-Schema-Upgrade.md) und des [Implementierungsplans](2026-10-09_Git-Generische-Server-Unterstuetzung_Implementierungsplan.md).

Status: weiterer GS02-Baustein implementiert. Generische Auftragsannahme und Ausführung bleiben deaktiviert; die vollständige GS02-Abnahme ist offen.

## Implementierte Grenze

`RepositoryBinding.ConnectionProfileId` ist optional. Bei fehlender Zuordnung wird die neue Eigenschaft nicht serialisiert; die bisherigen JSON-Verträge bleiben erhalten. `AssignConnectionProfileAsync` verlangt Adminrolle und frische Repository-Grants, überprüft beide erwarteten Revisionen und den Endpointbereich und erhöht die Bindungsrevision atomar. Repository-ID, Remote, bisherige Credentialreferenz, Modellroots und Branchregeln werden nicht umgeschrieben. Die Zuordnung ist eine eigene administrative Storeoperation; noch kein öffentliches API-/Studioformular.

Bei wartenden, laufenden, ungeklärten oder in Abgleich befindlichen Aufträgen wird die Zuordnung abgewiesen. Die relationale Serializable-Transaktion hält Prüfung und CAS zusammen. Fehlende, widerrufene oder fremde Profile werden nicht einem Defaultanbieter zugeordnet.

`CaptureConnectionTargetAsync` lädt Bindung, aktuelle Grants und das zugeordnete aktive Profil frisch. `ReadAcceptedExecutionTargetAsync` liest vorbereitete, verschlüsselte Zielbelege und prüft erneut Eigentümer, Grants, Operationstyp/Fähigkeit, aktuelle Profilzuordnung und vollständigen Zielkontext. Data-Protection-Purpose bindet diese Belege gesondert an Mandant, Actor, Repository und **Auftrags-ID**; Kopieren desselben Ciphertexts in einen anderen Auftrag schlägt fehl. Profiländerung oder Widerruf erlaubt keine Umleitung.

Migration `20261011140107_SourceControlExecutionContracts` ergänzt `ExecutionContractVersion` (Default 0) und `ProtectedExecutionTarget` (nullable) in der vorhandenen Tabelle. Alte Request-/Result-Ciphertexte, Fences, Leases und Zustände bleiben unverändert. PostgreSQL-/SQL-Server-Typen sind explizit; Schemaerzeugung allein ist keine Runtime-Abnahme dieser Provider.

Der aktuelle Worker unterstützt ausschließlich den vorhandenen **Ausführungsvertrag 0 ohne Zielbeleg**. Diese äußere Versionsgrenze ist getrennt von den bestehenden inneren Commit-/Push-/PR-JSON-Versionen. Jobauswahl, Claim-CAS und Ausführungsleser lehnen neue/inkonsistente Verträge ab. Die vorhandene aktive Annahme schreibt weiterhin Version 0; **Version 1 wird noch nicht aktiviert**.

Profilgebundene Bindungen werden von der bisherigen Auftragsannahme, den alten Ausführungslesern und dem GitHub-App-Broker mit `ProviderUnavailable` abgewiesen. Dadurch kann ein Basic-/SSH-/anderes Hostingprofil nicht unbeabsichtigt über die bisherigen GitHub-Credentials ausgeführt werden. Historische Ergebnisabfragen bleiben erreichbar.

## Rolloutpflicht

Die Claimgrenze schützt Worker mit **dieser neuen Implementierung**. Frühere Binärdateien kennen die neuen Felder nicht und könnten solche Aufträge weiterhin claimen. Eine neue Spalte allein kann das nicht verhindern. Vor Aktivierung neuer Annahme sind daher alle alten Worker zu drainen/zu stoppen oder zuerst auf diese kompatible Zwischenversion zu aktualisieren. Ein API-Rollback ersetzt diesen Schritt nicht. Neue Aufträge dürfen erst nach dieser Pflicht und angeschlossenem Generic-Executor freigegeben werden. Kein behaupteter Nachweis alter Binärkompatibilität durch bloße Simulation der neuen Claim-Methode.

## Verifikation

Neue SQLitefälle prüfen Zuordnung und Revisionskonflikte, aktive/unklare Altaufträge, echte Fremdtenant-Profile, Grants, Widerruf, unveränderte Altserialisierung, Ablehnung unbekannter Vertragsversionen ohne Receipt-/Leaseänderung sowie verschlüsselte Zielbelege nach neuem DbContext und Schutz vor Wiederverwendung unter einer anderen Auftrags-ID.

Der echte Upgrade-Test schreibt Altaufträge mit ausschließlich den **alten Spalten** vor der neuen Migration, anschließend prüft er unveränderte Daten plus Vertrag 0/fehlenden Zielbeleg. Kein BPMN-/DMN-Fixturewechsel, keine abgeschwächte Assertion oder neue CI-Ausnahme. SQL-Erzeugung für PostgreSQL/SQL Server prüft zusätzlich die additive Vertragsmigration ohne Drop/Update.

Strenger Solution-Build bestanden: **0 Warnungen, 0 Fehler**, 30,67 Sekunden. Gezielte Suite: **175 bestanden**, keine Fehler/Skips, 7,529 Sekunden; darin 16 neue Zuordnungs-/Vertragsfälle. Check-only-Formatprüfung der neuen handgepflegten Dateien, des erweiterten Vertrags und des Upgrade-Tests: Exit 0. `git diff --check`: Exit 0. Native Helper-/TLS-/Commit-/Push-/PR-/Lease-Regression: **157 bestanden**, keine Fehler/Skips, 222,144 Sekunden. Echte lokale Gitprozesse und TLS-Testserver, bestehender GitHub-Lease-/Prepared-Workspace-Pfad; keine externe Azure-/GitHub-/SSH-/PostgreSQL-Abnahme. CI-safe-Gesamtsuite anschließend separat bestanden: **1.566 bestanden, 0 Fehler, 8 externe Skips**, 1 Minute 28,021 Sekunden, Exit 0. Der zuvor sporadische C.8.0-Fehler trat in diesem Lauf nicht auf; seine Ursache ist weiterhin nicht geklärt. Lokale Logs: `artifacts/git-generic-servers/gs02-bindings-*.log`.

Clean-Code-Prüfung mit `.agents/skills/dotnet-clean-code-review/SKILL.md`, beiden Referenzen und D1–D7 über 33 betroffene handgepflegte C#-Dateien; generierte Migrationdesigner und Snapshots separat geprüft. Neue Migration ebenfalls manuell überprüft. D8: neue Methoden und geänderte Ausführungspfade gelesen. Kein bestätigter Wartbarkeitsbefund; I/O reicht Cancellation weiter, bool-Rückgaben sind CAS-Ergebnisse, keine neuen Interfaces oder Produktabhängigkeiten. Log `gs02-bindings-clean-code.json`.

## Reproduzierbare Prüfungen

Prüfbefehle (Editorassets unverändert; kein Restore):

```powershell
dotnet build VertexBPMN.sln --configuration Release --no-restore -p:SkipBpmnIoAssetBuild=true -m:1 --disable-build-servers
dotnet tests/VertexBPMN.Tests/bin/Release/net10.0/VertexBPMN.Tests.dll -class '*SourceControlConnectionBindingTests' -class '*SourceControlConnectionProfilePersistenceTests' -class '*RepositoryConnectionProfileTests' -class '*SourceControlExecutionTargetTests' -class '*LegacyGitHubConnectionMappingTests' -class '*SourceControlSecurityTests' -class '*SourceControlOptionsTests'
$env:VERTEXBPMN_TEST_GIT = (Get-Command git).Source
dotnet tests/VertexBPMN.SourceControl.Tests/bin/Release/net10.0/VertexBPMN.SourceControl.Tests.dll -class '*CommitExecutionAcceptanceTests' -class '*GitHttpsTransportTests' -class '*NativeGitBoundaryTests' -class '*LeaseRunnerTests' -class '*BasicCredentialChannelTests' -class '*CredentialChannelAcceptanceTests' -class '*SourceControlContractTests' -class '*HttpsBoundaryTests' -class '*GitHubPullRequestTransportTests' -class '*GitHubPullRequestResponseTests' -class '*GitHubPullRequestReconciliationTests'
```

CI-safe: exakter Befehl und unveränderte Ausschlussliste aus `.github/workflows/ci.yml`, Schritt **Test (CI-safe suite)**. Kein gesamter ungefilterter Testlauf.

## Nächste Arbeit

Die tatsächliche neue Annahme muss Requestidentität/Idempotenz einschließlich Zielkontext binden und Ciphertexte mit derselben Auftrags-Purpose erzeugen. Commit-/Push-/PR-Reconstruction und Reconciliation müssen diesen Kontext bis unmittelbar vor Effekten erneut prüfen. Neue Versionen werden erst zusammen mit ihrem Executor und dem beschriebenen Worker-Rollout aktiviert. Ein automatisches Legacy-GitHub-Datenupgrade wurde nicht ausgeführt.

Die nächsten Transportbausteine bleiben GS03 (explizite Netzwerk-/Trustpolicy einschließlich realer Peerprüfung) und GS04 (vollständiger eigener HTTPS-Server ohne Hostingzwang). Bearer, SSH, Azure DevOps und andere Hostingadapter bleiben gesonderte offene Abnahmen. Kein Commit, Push, Deployment oder Migration einer Benutzerdatenbank erfolgt.
