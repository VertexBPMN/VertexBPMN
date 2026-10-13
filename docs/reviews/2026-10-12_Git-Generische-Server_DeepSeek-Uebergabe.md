# Übergabe an DeepSeek: generische Git-Server

Stand: 12. Oktober 2026. Arbeitsbranch: `codex/git-generic-servers`, Ausgangscommit `02ebe35`. Dieser Branch ist ein Zwischenstand zur Fortsetzung, keine abgeschlossene Anbieter- oder Produktionsfreigabe.

## Auftrag und Einstieg

Der Nutzer möchte das Git-Plugin von der GitHub-Kopplung lösen, eigene Git-Server sowie Azure DevOps und weitere Anbieter unterstützen und Wiki/Website mit einem erweiterbaren Plug-in-Katalog pflegen. Die Implementierung soll ohne wiederholte Rückfragen weitergeführt werden. Der aktuelle Abschlussauftrag autorisiert Commit und Push dieses Übergabestands. Merge, Deployment, produktive Migrationen, Cloudprovisionierung und externe Testwrites sind nicht Bestandteil der Übergabe.

Zuerst vollständig lesen:

1. `AGENTS.md`, `README.md`, `global.json`, `Directory.Build.props`, `.editorconfig` und `.github/workflows/ci.yml`.
2. `docs/reviews/2026-10-09_Git-Generische-Server-Unterstuetzung_Implementierungsplan.md` – maßgeblicher Plan, einschließlich GS-T01–GS-T18.
3. `docs/reviews/2026-10-05_Git-Modellversionierung_Implementierungsplan.md` und `docs/reviews/2026-10-08_Git-G07_PR-Antwortvalidierung.md` für bestehende Grenzen und GitHub-Regressionspfade.
4. `docs/runbooks/git-connection-profiles.md` und die GS04-/GS05-/GS06-Berichte vom 12. Oktober.

Host und Checkout neu erkennen. Lokale Übergabe wurde unter Windows/PowerShell, .NET SDK 10.0.303, Git 2.56 durchgeführt. Auf einer Linux-VM keine Windowspfade oder `wslc.exe` voraussetzen. Keine SDK-/Toolinstallation oder Azure-/Dockerpflicht aus dieser Übergabe ableiten.

## Was bereits angeschlossen ist

| Bereich | Implementierter Stand | Grenze |
| --- | --- | --- |
| Profile/Persistenz | Tenantgebundene Profile, Revision/CAS, Widerruf, Repositoryzuordnung; additive Migrationen | Keine Betriebsdatenbank aktualisiert |
| Generisches HTTPS | Basic/PAT, externes Bearer, anonymes Lesen; Branch/File/History/Diff, Commit/Push, dauerhafte Ergebnisse | Gesamtstufe A noch nicht real mit API/Studio/Server/Recovery abgenommen |
| Netzwerk/Trust | Administrative Scope-/CIDR-/CA-Profile; alle DNS-Antworten geprüft; native Peer-Pinning; keine Redirects/Umgebungsproxies | Linux und kombinierte echte Infrastrukturabnahme offen |
| Credentials | Frische Actor-/Grant-/Profil-/Metadatenprüfungen, Ablauf, Rotation und geschützter Helperkanal | Kein Basic-Fallback bei Bearer; keine Secrets in Argumenten/Umgebung/Gitkonfiguration |
| Azure Entra | Explizite Client-Secret-Anwendungsidentität, feste DevOps-Audience, ausschließlich `dev.azure.com`/HTTPS/Standardport | Issuer in lokalen Regressionen ersetzt; keine echte Azure-Abnahme; PR-Adapter fehlt |
| Hostingregistry | Bestehender GitHub-App-Create/Read/Reviews/Reconciliation-Pfad hinter fester Registry | Kein dynamisches DLL-/Typnameloading |
| Neue profilgebundene Hostingarbeit | Eigener Vertrag v3, API-/Repositoryidentität, Inhaltsbindung, Claiming, Intent/Abgleich, geschützte Ergebnisidentität; erster GitHub-Profilpfad | Jüngste Änderungen erst im Übergabeprüflauf qualifizieren; weitere Provider fehlen |
| API/Studio | Profile/Zuordnung/Grundfunktionen und ressourcenbezogene Fähigkeiten angeschlossen | Browser-/Tenant-/Dirty-/Providerwechsel-Abnahme und vollständige Hostingbedienung offen |

## Genau hier wurde die Arbeit fortgesetzt

### Neue Hostingverträge

- `src/VertexBPMN.SourceControl.Abstractions/RepositoryHostingTarget.cs`, `IRepositoryHostingTargetResolver.cs`.
- `src/VertexBPMN.Application/SourceControl/RepositoryHostingTargetPolicy.cs` und `RepositoryHostingReceiptPolicy.cs`: aktuell nur explizite GitHub-Zuordnung. Andere Provider werden abgewiesen; dies ist kein generischer REST-Adapter.
- `src/VertexBPMN.Infrastructure/SourceControl/AcceptedHostingExecution.cs`, `HostingExecutionProtection.cs`, `HostingHttpsContext.cs`, `PersistentSourceControlStore.HostingExecution.cs`.
- Bestehende PR-Annahme/-Intent/-Abschluss-/Views-/Executor-/Statusreader-/Workerpfade wurden erweitert. `StoredPullRequest` nutzt Schema 2 mit Hostingziel für neue Profilaufträge; Altbelege bleiben Schema 1.
- `ProfiledHostingCredentialBroker.cs`, `GitHubHostingAdapter.cs`, `SourceControlHttps.cs`: explizites Hosting-Credential `HostingHttpBearer` mit `token`/`expiresAt`, API-Netzwerkfreigabe vor Secretauflösung, operationslokale Peer-Pinning-/Repositorygrenze. GitHub-Profiladapter verwendet Systemtrust; private API-CA ist hier ausdrücklich nicht aktiviert.

### Versionsgrenze erhalten

| Ausführungsvertrag | Verwendung |
| --- | --- |
| 0 + kein geschütztes Ziel | Qualifizierte Legacy-GitHub-Jobs |
| 1 | Frühere geschützte Zielbausteine; nicht als aktive generische Workeraufträge behandeln |
| 2 | Profilgebundene HTTPS-Read-/Commit-/Push-Ausführung |
| 3 | Neue profilgebundene PR-Ausführung mit getrenntem Hostingziel und API-Policy |

`HostingExecutionProtection` bindet Tenant, Repository, Actor, Operation-ID, Kind, Idempotenzschlüssel und Anfragehash in den Data-Protection-Purpose. Der Snapshot enthält Profil/API-/Netzwerkidentität sowie Inhaltsdigest; keine Secretwerte. Alte Worker dürfen v2/v3 nicht übernehmen. Neue Hostingprofile nach Annahme nicht ungeprüft an laufende Aufträge anpassen.

Die zusätzliche Claimverwendung von `ClaimProfiledWriteRowAsync` bindet die zuvor geprüfte konkrete Vertragsversion aus der Zeile; das Einfallstor bleibt jeweils der typisierte v2-/v3-Claimpfad. Nicht auf beliebige Versionen erweitern. Nach Antwortverlust oder Leaseablauf niemals einen zweiten Create ausführen.

### Neue Tests und noch notwendige Ergänzungen

`RepositoryHostingTargetTests`, `RepositoryHostingReceiptTests` und `SourceControlConnectionBindingTests.Hosting.cs` prüfen Ziel-/Antwortidentität, separate Workergrenzen, Idempotenz, Belegreplay, Rollen-/Profil-/Netzwerkwechsel und Abgleich nach verlorenem Create. Hosting-Recovery benutzt einen kontrollierten Adapter; Commit-/Push-Setup persistiert kontrollierte lokale Ergebnisse. Das ersetzt keinen realen Transportnachweis.

Als Nächstes zusätzliche Tests ergänzen für:

- `ProfiledHostingCredentialBroker`: falscher Tenant/Typ/Referenz, Ablauf vor Tokenauflösung, Rollen-/Profil-/Metadatenänderung während Auflösung, Fehlersanitisierung und Cancellation.
- Neuer `SourceControlHttps.CreateProfiledGitHubClient`: fremder Repositorypfad, fremder Host/Port, zulässige Pagingqueries, Redirects, tatsächlicher DNS-to-Socket-Pfad und Cleanup.
- Neues Hosting-Claiming: Quoten, gleichzeitige Claims, stale Fence, Provider-/API-/Trusttausch, beschädigte und zurückgestufte Verträge.
- Entra-SDK-Pfad: echte Tokenanforderung einschließlich Authority-/Netzwerk-/Proxyverhalten und Abbruch prüfen; die bisherigen Issuer-Doubles qualifizieren diesen SDK-Netzwerkpfad nicht.
- Historischer Beleg nach Profilwiderruf bleibt abrufbar; Liveabfrage verweigert widerrufene/retargetete Profile. Keine gespeicherte Erstellungsquittung mit Livezustand überschreiben.
- Registry/DI und API/Studio durch den tatsächlichen Produktpfad; öffentliches `IRepositoryHostingProvider` ist noch nicht vollständig in diesen dauerhaften Auftragspfad integriert.
- Audit: unveränderliche Auftragsbelege unterscheiden Vertex-Actor, Gitquelle und Hosting-Credentialreferenz. Eine vollständige Einbindung in den zentralen Auditpfad ist noch zu prüfen; vorhandener `AuditLogService` schreibt über `ProcessMiningEventDbContext`, daher keine atomare Bpmn-Transaktion vortäuschen.

## Prüfergebnisse

Die früheren 406 gezielten Erfolge und CI-safe 1.777 Erfolge/acht Skips gehören zum Stand **vor** der jüngsten Hosting-v3-Ergänzung. Sie dürfen nicht als Nachweis der neuen Hostingarbeit übernommen werden. 131 native Windows-Fälle stammen ebenfalls aus einem früheren Kanalstand; `SourceControlHttps` wurde inzwischen weiterentwickelt.

Der erste aktuelle Übergabebuild fand noch einen MA0051-Befund in `InsertOrReconcileAsync`. Die Ausführungskontextbindung wurde daraufhin in `BindAcceptedExecution` extrahiert; keine Analyzerabschaltung. Anschließend wurden Testkompilierungsfehler korrigiert: vierteilige bestätigte Hostingquittung, explizite Erfolgsassertion und ein Parametername für URI-Testeingaben. Ein negativer URL-Fall war semantisch dieselbe kanonische Repositoryadresse und wird nun positiv auf gleiche Zielidentität geprüft. Der neue kontrollierte Commitbeleg enthielt zunächst die Repositoryrevision statt des Workspace-Fence; die Testvorbereitung verwendet jetzt den tatsächlichen Claim-Fence 1. Bestehende Conformancefixtures und Schutzassertions wurden nicht abgeschwächt.

Aktuelle Prüfungen nach diesen Korrekturen:

- Strenger Release-Gesamtbuild nach der abschließenden Größenlimitkorrektur: **bestanden, keine Warnungen/Fehler**, 1:01,49; unveränderte Editorassets übersprungen.
- Gezielte Profil-/Security-/Persistenz-/Hosting-Auswahl: **447 bestanden**, keine Fehler/Skips, 39,410 Sekunden. Ein erster Durchlauf hatte acht Fehler aus den genannten neuen Vorbereitungs-/URLannahmen; die korrigierte Auswahl bestand zunächst mit 445 Fällen. Der abschließende Review korrigierte zusätzlich ein zu kleines Limit des geschützten PR-Requests: 16 Ki-Zeichen Beschreibungen können in JSON und Base64 wachsen. Die Grenze beträgt jetzt 256 Ki-Zeichen für den geschützten Umschlag; maximal lange ASCII- und Unicodebeschreibungen werden angenommen, geclaimt und unverändert rekonstruiert.
- Bestehende GitHub-Transport-/Antwort-/Reconciliation-Regressionsauswahl im separaten Source-Control-Testprojekt: **31 bestanden**, keine Fehler/Skips, 0,541 Sekunden.
- Check-only-Whitespace für die zwei neuen Applicationpolicies, acht aktuelle Hosting-Infrastructuredateien und drei neue Testdateien: bestanden. Andere vorhandene Dateien wurden nicht pauschal umformatiert.
- Clean-Code-Prüfung: D1–D7 für 27 Dateien der jüngsten Hostingarbeit ausgeführt, keine Suchfehler; betroffene Methoden zusätzlich auf Cancellation, Lifetimes, Vertragsgrenzen und Lesbarkeit geprüft. Neue Adapter-/Resolverinterfaces bilden echte Hosting-/Zielgrenzen mit Produktionsimplementierung und Testsubstitution. Die DI-Registrierung, Fixtureerweiterung und Tuple-Testkorrektur wurden gezielt mitgeprüft. Kein kompletter Clean-Code-Nachweis für alle 166 übergebenen Dateien; deren übrige Teilprüfungen stehen in den vorherigen Paketberichten.
- Finale CI-safe-Auswahl nach der Größenlimitkorrektur: **1.818 bestanden, keine Fehler, acht bestehende externe Skips**, 1.826 insgesamt, 1:45,672. Vorheriger Lauf: 1.816 Erfolge/acht Skips, 1:51,549. Kein Nachweis einer Behebung des sporadischen FEEL-Timeouts; keine externe Anbieter-/Browser-/Linuxabnahme.
- `git diff --cached --check`: bestanden nach Entfernung zweier zusätzlicher Leerzeilen am Dateiende in vorher unversionierten GS04-Dateien. Diese abschließende Korrektur verändert keine Programmlogik.

Lokale Logs unter `artifacts/git-generic-servers/deepseek-handoff-*.log` sind ignoriert und werden nicht gepusht. Die dokumentierten Befehle und Ergebnisse ermöglichen einen unabhängigen Wiederholungslauf.

## Befehle für die Fortsetzung

```powershell
git status --short
git branch --show-current
dotnet build VertexBPMN.sln --configuration Release --no-restore -p:SkipBpmnIoAssetBuild=true -m:1 --disable-build-servers

dotnet tests/VertexBPMN.Tests/bin/Release/net10.0/VertexBPMN.Tests.dll -class '*RepositoryHostingTargetTests' -class '*RepositoryHostingReceiptTests' -class '*RepositoryHostingRegistryTests' -class '*SourceControlConnectionBindingTests' -class '*SourceControlPersistenceTests'
```

Restore nur bei Bedarf und erlaubtem Netzwerkzugriff. Editorassets sind unverändert; daher ist der vorhandene Asset-Skip in diesem Stand zulässig. Vor `--no-build` einen aktuellen erfolgreichen Build sicherstellen. Runneroptionen gegebenenfalls mit `-?` prüfen; xUnit-In-Process verwendet `-list tests`, nicht `--list-tests`.

CI-safe exakt entsprechend `.github/workflows/ci.yml`:

```powershell
dotnet test tests/VertexBPMN.Tests/VertexBPMN.Tests.csproj --configuration Release --no-build --no-restore --filter-not-trait 'Category=Phase3ExternalAcceptance' --filter-not-trait 'Category=Phase4OutageAcceptance' --filter-not-trait 'Category=Phase5RecoveryAcceptance' --filter-not-trait 'Category=Phase6LoadAndSoakAcceptance' --filter-not-trait 'Category=Phase7ConformanceAcceptance' --filter-not-trait 'Category=Phase8MonitoringAcceptance' --filter-not-trait 'Category=ExternalTaskPostgres' --filter-not-trait 'Category=ExternalAgentProcessRecovery' --filter-not-trait 'Category=ContractReviewOllama' --filter-not-trait 'Category=ContractReviewBenchmark' --filter-not-class 'VertexBPMN.Tests.Conformance.DistributedConformanceTests' --max-parallel-test-modules 1
```

Native Tests bleiben Opt-in im separaten Projekt `tests/VertexBPMN.SourceControl.Tests`. Unter Windows nutzt die bestehende Auswahl `$env:VERTEXBPMN_TEST_GIT=(Get-Command git).Source`; auf Linux an den tatsächlichen Gitpfad anpassen. Keine externen Dienste in gewöhnliche PR-Tests einbauen. Für Änderungen abschließend `.agents/skills/dotnet-clean-code-review/SKILL.md` auf den betroffenen Umfang anwenden; Formatierung nur check-only.

## Verbleibende Arbeit in Reihenfolge

1. **GS05 abschließen:** aktuelle Hostingarbeit streng bauen und testen; genannte Broker-/HTTP-/Lifecycle-/Auditlücken schließen; GitHub-Regressionspfad behalten. Zuordnungen, Anfrageversionen und sichere Unknown-Reconciliation zuerst stabilisieren.
2. **GS06 Azure Services:** Organisations-/Projekt-/Repositoryidentität, feste API-Version, getrennte PAT-/Entra-Pfade; PR Create/Read/Reconciliation, Zustände/Votes/Iterationen, Commitbezüge und unvollständige Policies. Offizielle aktuelle REST-Verträge prüfen. Keine automatische Releasefreigabe.
3. **GS07 SSH:** feste ausführbare Datei, Strukturierung/Normalisierung, tatsächlich gepinnter Peer, Known-Hosts-Trust, dienstgebundener Schlüssel/Passphrase-IPC, Rechte/Cleanup, Prozessbaum-Cancellation. Kein Benutzeragent, ProxyCommand, Forwarding oder TOFU.
4. **GS08 Anbieter:** GitLab, Gitea und Forgejo mit getrennten Registry-IDs, GitHub Enterprise und Azure DevOps Server jeweils mit expliziten Versionen und qualifizierten Ziel-/Auth-/Antwortverträgen. Keine Annahme gleicher APIs aus Produktverwandtschaft.
5. **GS09 inkrementell:** vollständige PR/MR-/Capability-Bedienung, eigene Belege, Provider-/Tenantwechsel und verspätete Antworten; Dirty/Undo/Export erhalten.
6. **GS10:** eigenständige Windows-/Linux-/Server-/API-/Studio-/Recovery-Abnahmen, Betriebsrunbook, Wiki/Website/Supportmatrix ausschließlich nach tatsächlichen Nachweisen. Externe Pflicht-Skips offen halten.

## Bekannte Störung außerhalb der Git-Änderungen

Breite Läufe zeigten sporadisch C.8.0 `Vacation Approval`. Die ergänzte Testdiagnose in `tests/VertexBPMN.Tests/Conformance/C_8_0_Test.cs` weist die vorgelagerte Ausnahme `Jint.Constraints.TimeConstraint.Check` bei `vertexFeelEvaluate` aus. Danach meldet das Gateway die fehlende Variable. Ursache dieses Fehlverlaufs ist ein FEEL-Timeout; die Last-/Laufzeitstabilität ist nicht behoben. Grüner Wiederholungslauf ist keine Behebung. Keine gültigen Fixtures verändern, Assertions schwächen, Fälle überspringen oder das Zeitlimit ungeprüft erhöhen.

## Weitere Repositorys und lokale Ausschlüsse

- Wiki: `VertexBPMN.wiki.git`, Branch `codex/git-generic-servers-docs`, Dateien `Git-Plugin.md`, `Plug-ins.md`.
- Website: `VertexBPMN_Website.git`, Branch `codex/git-generic-servers-docs`, Datei `plugins.html`.
- Push der Dokumentationsbranches ist keine Veröffentlichung des Wikis oder der Website. Keine automatischen Änderungen an deren Standardbranch.
- Lokale fremde Dateien `docs/architecture/2026-10-11_Keycloak-Passkeys_Umsetzungsplan.md`, `engine-current-compiler-arguments.json`, `tests-current-compiler-arguments.json` wurden vom Git-Auftrag ausgeschlossen. Nicht löschen, veröffentlichen oder Inhalte der Compilerargumente ausgeben. Sie sind nach frischem Clone nicht Teil dieses Branches.

## Starttext für DeepSeek

> Setze die generische Git-Server-Unterstützung auf `codex/git-generic-servers` fort. Lies AGENTS.md, diese Übergabe und den vollständigen Implementierungsplan. Prüfe den tatsächlichen Checkout und die aktuellen Prüfergebnisse. Schließe zuerst die profilgebundene Hosting-v3-Arbeit einschließlich sicherer Credentials, HTTP-Zielbindung, Idempotenz/Unknown-Recovery und Audit ab; danach GS06, GS07, GS08 und die begleitende Studio-/API-Arbeit. Erhalte Legacy-GitHub, tenantgebundene Rechte, dauerhafte Belege, Ref-CAS und lokale Profile. Verwechsle vorbereitete Tests und grüne Unit-Tests nicht mit echter Anbieter-/Browser-/Recovery-Abnahme. Keine Secrets ausgeben, keine Analyzer-/Conformanceabschwächung, kein automatischer Merge/Deployment, keine Cloudressourcen oder externen Testwrites. Dokumentiere pro Paket tatsächliche Dateien, Befehle, Ergebnisse und offene Pflichtgates.
