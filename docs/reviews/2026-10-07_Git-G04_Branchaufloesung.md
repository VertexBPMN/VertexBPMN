# G04 – Branch auf unveränderliche Revision auflösen

Basis: `63a20b5`, Branch `codex/git-source-control-phase-1`.
Status: interner Adapter implementiert; keine vollständige Provider-/API-Abnahme.

## Umsetzung

`ControlledGitProcess.Revisions.cs` ergänzt die authentifizierte Auflösung genau
eines validierten `refs/heads/<branch>` in eine `RevisionSelection`. Anders als
Push dürfen Default- und Releasebranches gelesen werden. Tenantvergleich erfolgt
vor Netzwerk-/Git-Zugriff. Fehlende Branches liefern `NotFound`; weder HEAD noch
ein lokaler Cache oder ein anderer Branch wird als Ersatz gewählt.

Produktionszugriff verwendet bestehende HTTPS-Allowlist, öffentliche DNS-Adressen,
DNS-Pinning, Auth-Helper, geprüfte TLS-Verbindung, begrenzte Ausgabe und Read-Timeout.
Die Loopback-/CA-Ausnahme bleibt ein expliziter interner Testeinstieg und ist kein
konfigurierbarer Produktionsmodus.

Die gemeinsame Head-Auswertung liegt in `GitRemoteReferences.ReadHead`, der feste
Git-Befehl in der revisionsbezogenen Partial-Datei. Push verwendet dieselbe
Leselogik, behält aber sämtliche eigenen Write-/Branch-/Fast-Forward-Regeln.
Ungültige UTF-8-Ausgabe, verkürzte/Null-IDs, falsche Ref, unvollständige oder mehrfache
Antwortzeilen werden abgelehnt. Eine leere Antwort bedeutet nur „Branch fehlt“.

Keine neue öffentliche Schnittstelle, Migration, Abhängigkeit, Hostingmutation
oder Studio-Änderung. Kontext ist **keine** ACL: Der spätere Produktprovider muss
weiterhin die aktuelle Bindung, Rollen und Repositoryberechtigungen serverseitig
auflösen; der interne Adapter ersetzt diese Prüfung nicht.

## Tests

Neue Dateien:

- `tests/VertexBPMN.SourceControl.Tests/GitHttpsTransportTests.Revisions.cs`
- `tests/VertexBPMN.SourceControl.Tests/GitRemoteHeadTests.cs`

`Resolved_remote_branch_pins_model_bytes_across_head_changes` prüft mit realem
HTTPS-Git-Backend SHA-1 und SHA-256: Auswahl stimmt zum Remote-Head, nach Änderung
des Branches wird der feste ausgewählte Commit gefetcht und bytegetreu gelesen;
erneute Auswahl zeigt den neuen Stand. Kein Push erfolgt. Fremdtenant, ungültige
Branches und bereits angeforderte Cancellation verursachen keinen weiteren
authentifizierten Remotezugriff. Produktionspfad lehnt das lokale Testziel ab.

`Exact_head_is_required_and_absent_head_is_not_invented` prüft beide OID-Formate
und die redigierten Parser-Negativfälle ohne externe Infrastruktur.

Erster gezielter HTTPS-Lauf: zwei bestanden, kein Fehler/Skip, 15,716s.
Lokale Adaptersuite ohne `*PostgresAcceptanceTests`: **120 bestanden, 0 Fehler,
0 Skips**, 4m27,108s. Ausgeführt nach aktuellem Build, vor der abschließenden
rein strukturellen Trennung des gemeinsamen Lesecodes.

Nach dieser Trennung erneut gebaut und gezielt geprüft:

- `--filter-class '*GitRemoteHeadTests'`: zwei bestanden, 1,104s.
- `--filter-method '*Resolved_remote_branch*'`: zwei bestanden, 15,612s.
- `--filter-method '*Https_push_creates_only_workbranch*'`: zwei bestanden, 26,235s.
- Alle sechs Fälle: kein Fehler und kein Skip.

MTP-Basis für die Adapterläufe:

```powershell
$env:VERTEXBPMN_TEST_GIT = (Get-Command git).Source
dotnet test tests/VertexBPMN.SourceControl.Tests/VertexBPMN.SourceControl.Tests.csproj --configuration Release --no-build --no-restore --filter-not-class '*PostgresAcceptanceTests' --max-parallel-test-modules 1 --timeout 10m
```

Gezielte Läufe ersetzen den Klassen-Ausschluss durch den oben angegebenen Filter
und verwenden `--timeout 5m`. Keine Erweiterung der CI-Ausschlussliste.

CI-safe Hauptsuite gemäß unverändertem Workflow, nach abschließendem
Hauptprojektbuild: **1.393 bestanden, 0 Fehler, acht bestehende Opt-in-Skips**,
1m40,757s. Build/Testprotokolle unter `%TEMP%/vertex-branch-*.log`.

Die Diagnosebuilds verwenden `--configuration Release --no-restore`,
`-p:SkipBpmnIoAssetBuild=true -p:RunAnalyzers=false -p:EnforceCodeStyleInBuild=false
-p:TreatWarningsAsErrors=false -m:1 --disable-build-servers`. Abschließender
Adapterbuild: 0 Fehler/Warnungen (inkrementell); Hauptprojektbuild: 0 Fehler,
337 bestehende Warnungen. **Kein erfolgreicher strenger Analyzer-Build behauptet.**
Check-only Formatprüfung aller fünf C#-Dateien und `git diff --check` bestanden.

## Clean-Code-Prüfung

Skill `dotnet-clean-code-review`, fünf C#-Dateien, nur neue/geänderte Mitglieder
und deren bestehende Aufrufer. D1–D7 durchgeführt; manuelle Lesbarkeitsprüfung.

| Regel | Ergebnis |
| --- | --- |
| Async/Cancellation | geprüft; Token bis zum kontrollierten Git-Prozess weitergereicht |
| Collections | keine neuen Collection-Rückgabeverträge |
| Boolean-Modi | keine neuen Boolean-Modusschalter |
| Verantwortungsnamen | gemeinsame Head-Leselogik aus Push-Teil herausgelöst |
| Regionen | keine neuen Regionen |
| Interfaces | bestehende Providergrenze erhalten, keine neue Schnittstelle |
| Vererbung | Partial-Dateien, keine neue Vererbung |
| Lesbarkeit | keine bestätigten neuen Befunde; check-only Formatprüfung bestanden |

## Offene Grenzen und nächster Schritt

Produktproviderregistrierung, autorisierter Lese-Job-/Workspace-Lifecycle, API,
Studio, Live-GitHub/Linux und aktuelle Rollenauflösung für Hintergrund-Writes
bleiben offen. Keine G04-Gesamtcheckbox geschlossen. Nächster Schritt ist der
autorisierte Leseprovideranschluss an Persistenz, Credentials und private
Arbeitsbereiche; kein direkter Browserzugriff auf diesen internen Adapter.
