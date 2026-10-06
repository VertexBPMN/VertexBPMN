# G02/G03 – Echter HTTPS-Transport und typisierte Commit-Annahme

Stand: 2026-10-05; Branch `codex/git-source-control-phase-1`, auf `59c131d` aufbauend. Änderungen noch nicht committed/published. G02/G03 bleiben insgesamt offen; SQL Server weiterhin auf Nutzerwunsch nicht getestet.

## Behobene Ursachen und Umsetzung

- `src/VertexBPMN.SourceControl.AuthHelper/Program.cs`: Das Git-Credential-Protokoll besitzt wiederholbare Metadatenarrays. Der bisherige Dictionary-Parser wies echte Git-Anfragen deshalb vor der Pipe-Verbindung ab. `capability[]`, `wwwauth[]` und `state[]` werden nun begrenzt eingelesen, aber weder als Identität verwendet noch gespeichert/ausgegeben. Doppelte Identitätsfelder und vorgetäuschte Passwörter bleiben abgelehnt. Grundlage: [Git-Credential-Protokoll](https://git-scm.com/docs/git-credential).
- `GitCredentialChannel.cs`: Die Smart-HTTP-Schreibweise mit genau einem abschließenden Slash wird für dasselbe Repository akzeptiert. Fremde Hosts, Repositorys und Unterpfade bleiben gesperrt.
- `ControlledGitProcess.cs`: feste nicht geheime Richtlinien über `-c`/ArgumentList, Shell-Helperpräfix für den vertrauenswürdigen internen Helper; kein Token in Argumenten oder Environment. TLS-Prüfung, strikte Schannel-Revocationprüfung, DNS-Pinning und Redirect-/Proxy-Sperre bleiben aktiv. Bundle-URI-Downloads ausdrücklich deaktiviert. Loopback/Test-CA nur über internen isolierten Abnahmeadapter, keine konfigurierbare Produktionsausnahme.
- `SourceControlWorkspace.MeasureBytes`: Git entfernt/benennt temporäre eigene Dateien während der Quotenmessung um. Nur `FileNotFoundException`/`DirectoryNotFoundException` für verschwindende Einträge werden toleriert; fehlende Root, Reparse Points, Zugriffsfehler, Überlauf und überschrittene Quoten bleiben Fehler. Kein Retry/Skip eines fehlgeschlagenen Git-Tests zum Verdecken dieses Rennens.
- `tests/VertexBPMN.SourceControl.Tests/GitHttpsTransportTests.cs`: echter lokaler TLS-Server mit dynamischem Loopback-Port, eigener CA, SAN/Server-Auth-Zertifikat und signierter, per Loopback-HTTP bereitgestellter Sperrliste. Zertifikate bleiben während der Serverlaufzeit gültige Handles. Keine Truststore-Installation und keine TLS-/Revocation-Abschaltung. PKI-API: [Microsoft CRL Builder](https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.x509certificates.certificaterevocationlistbuilder?view=net-10.0).

Temporäre Diagnosecallbacks/Preflight-Aufrufe vollständig entfernt. Produktcode verwirft begrenztes Provider-stderr weiterhin, statt möglicherweise geheime Details zu loggen.

## Typisierte Commit-Annahme

- `src/VertexBPMN.Application/SourceControl/CommitJobSubmission.cs`: bestätigte Sessionrevision, Basiscommit, Arbeitsbranch, Nachricht und immutable ModelSnapshots.
- `PersistentSourceControlStore.EnqueueCommitAsync`: aktuelle Rollen/Repository-ACL, Eigentümer/Tenant, Sitzung, Revision, Basiscommit, Generation, Dateirevision und exakte gespeicherte Bytes prüfen. Nur erlaubte BPMN-Pfade/Inhalte und der sitzungsgebundene Arbeitsbranch; kein Default-/Releasebranch, keine doppelten Case-kollidierenden Pfade oder ungespeicherten Bytes.
- Kanonischer, schema-versionierter Input mit deterministischer Pfadreihenfolge, SHA-256 und Bytes wird verschlüsselt in der bestehenden Jobtabelle angenommen. Unique-Constraint und Serializable-Transaktion wiederverwendet, keine neue InMemory-Queue oder Migration.
- Identischer Request bleibt nach N+1-Editoränderung idempotent; anderes Payload unter gleichem Schlüssel ergibt Konflikt. Ein neuer Auftrag für einen veralteten Sessionstand wird abgelehnt. N+1 bleibt eigenständiger uncommitteter Editorstand.
- Opaques `EnqueueAsync` ist nur noch interne Speicherprimitive. Öffentliche Produktaufrufer können darüber keine beliebigen unvalidierten Jobbytes einstellen. Typisierte Push-/PR-/Deploy-Annahme sowie ausführender/reconcilierender G04/G07/G08-Provider noch offen.

## Tatsächlich ausgeführt

Lokale separate Adapter-Suite: **62 bestanden, 0 fehlgeschlagen, 0 übersprungen**, 23,130 s. Die beiden PostgreSQL-Fälle wurden gezielt nicht ausgewählt und nicht als bestanden gerechnet; vorherige WSLC-Nachweise unverändert historisch.

```powershell
$env:VERTEXBPMN_TEST_GIT = (Get-Command git).Source
dotnet test tests/VertexBPMN.SourceControl.Tests/VertexBPMN.SourceControl.Tests.csproj --configuration Release --filter-not-class '*PostgresAcceptanceTests' --max-parallel-test-modules 1
```

Enthaltene echte Grenzen:

- HTTPS-Git-Fetch mit tatsächlichem Git-HTTP-Backend und Helper, Auth-Challenge, unabhängigem `git show` für CRLF-Bytes und Secret-Artefaktprüfung.
- Unvertraute CA führt vor jedem HTTP-Auth-Challenge zum Fehler, nicht nur zu irgendeiner Exception im Testsetup.
- Timeout und Anwenderabbruch erst nach nachgewiesenem authentifiziertem laufendem Request; Socket des Git-HTTPS-Childs wird geschlossen. Kein vorab gecancelter Token als laufender Abbruchnachweis.
- Planted reference-transaction Hook, Fsmonitor-/Filter-/Diffkonfiguration, `.gitattributes`, `.gitmodules` und echter Gitlink: geschützter Bare-Fetch führt sie nicht aus und erzeugt keinen Submodule-Checkout. Unabhängiger unsicherer `update-ref`-Kontrolllauf beweist, dass der Hook in der isolierten Fixture tatsächlich ausführbar ist.
- Acht native Helperfälle einschließlich wiederholbarer Metadaten, trailing Slash, fremdem Host/Repository/Unterpfad, doppeltem Host und Passwortinjektion; Apphost mit bereinigter Umgebung statt nur `dotnet helper.dll`.

SQLite-/Host-Persistenzklasse: **16 bestanden, 0 Fehler/Skips**, 11,587 s. Darin zwei neue typisierte Commitfälle plus die bisherigen Maintenance-/Persistenzfälle. Keine Assertions abgeschwächt. Aufträge werden geprüft/angenommen, nicht bereits als Git-Commit ausgeführt.

Finaler Solution-Build: **0 Fehler, 0 Warnungen im inkrementellen Lauf**, 5,75 s. Vorheriger umfassenderer Lauf hatte eine bestehende Benchmark-Analyzerwarnung; kein warning-freier Clean-Build behauptet.

Finale reguläre CI-safe Suite nach diesem Build, mit unveränderten Filtern aus `.github/workflows/ci.yml`: **1.388 gesamt, 1.380 bestanden, 0 fehlgeschlagen, 8 bestehende externe Opt-in-Fälle übersprungen**, 80,262 s. Darin die 16 SQLite-/Host-Persistenzfälle. Die separat lokal ausgeführten 62 Adapterfälle wurden nicht in den GitHub-Testschritt aufgenommen.

## Restumfang

G04: realer Commit/Push, erwartete Git-IDs/Hashes und verlorene Antworten abgleichen; unbekannte Writeeffekte nicht erneut blind ausführen. Weitere typisierte Annahmepfade, Linuxqualifikation, reale GitHub-App-/Remoteabnahme und Deployment-Provenienz bleiben offen. Kein kompletter Phase-2-/Produktreifeabschluss aus diesen Teilnachweisen. CI-Workflow/Filter unverändert; kein Native-Git-/Remote-/Browserzwang in regulären PR-Tests.
