# Phase 2 – Implementierung und verbleibende Pflichtabnahmen

Stand: 2026-10-05; Branch `codex/git-source-control-phase-1`, Basiscommit `4c30df17d7d819ab50c776cf326f12556d54e77d`. Der ursprünglich unten dokumentierte Teilstand wurde als `59c131d` committed und gepusht. Nachfolgende Änderungen noch nicht committed/published. **Kein vollständiger Phase-2-Abschluss behauptet.**

Aktualisierung: SQL Server wird auf ausdrücklichen Nutzerwunsch nicht getestet; kein Container und keine weitere Zugangsdatenanforderung. Das ist keine bestandene SQL-Server-Abnahme. Maintenance-Host, Crash-Erkennung/Fencing und sicherer Terminal-Workspace-Cleanup unter SQLite einschließlich echtem Hoststart geprüft; siehe [G03-Maintenance](2026-10-05_Git-G03_Maintenance.md). Der zuvor fehlgeschlagene echte HTTPS-Git-Gesamttest ist inzwischen behoben: lokale Adapter-Suite mit TLS/Helper, Hooks/Filter/Gitlink und laufendem Timeout/Cancel **62/62 bestanden**. Typisierte Commit-Annahme mit bestätigtem Snapshot ergänzt: [aktueller Transport-/Annahmebericht](2026-10-05_Git-G02_Transport-und-G03_Annahme.md). Die folgenden älteren Zahlen sind historische Teilnachweise, nicht die aktuelle Gesamtauswahl.

## Implementierte Ergänzungen

- `Infrastructure/SourceControl/ControlledGitProcess.cs`: feste Version-/Bare-Init-/HTTPS-Fetch-Kommandos; Git >= 2.56, ArgumentList ohne Benutzershell, bereinigte Umgebung, leere globale Konfiguration, leere Hooks/Templates, keine Checkouts/Filter/Submodule, TLS-Prüfung, keine Redirects/Proxies, öffentliche DNS-Adressen in libcurl gepinnt, begrenzte Ausgaben und Clonegröße, Timeout/Cancellation mit Prozessbaumbeendigung. Das ist die Prozessgrenze, noch kein vollständiger G04-Provider. Windows-Git lehnt `NUL` als Konfigurationsdatei ab; auf eine geprüfte leere Datei im privaten Control-Verzeichnis korrigiert.
- `SourceControl.AuthHelper/Program.cs`, `BoundedProtocol.cs`, `GitCredentialChannel.cs`: echte repositorygebundene, kurzlebige Same-Account-Pipe; begrenzte Zeilen vor Speicherwachstum, Cancellation gekoppelt, keine persistierenden store/erase-Aktionen, keine Credentialwerte in Prozessargumenten/Environment/Dateien. Vorgetäuschte Passwortfelder und abweichende Benutzernamen abgelehnt. OS-Administratoren und Prozesse desselben Kontos bleiben außerhalb dieser Schutzgrenze.
- API-Publish-Target liefert den Helper samt Runtimekonfiguration in `source-control-auth/` aus. API-Publish lokal erfolgreich; Dateien kontrolliert. Im lokalen Development-Host kann `SourceControl:AuthHelperExecutablePath` auf den separat gebauten Helper zeigen. Deaktivierter Modus bleibt ohne Git-/Credentialanforderung.
- `PersistentSourceControlStore.cs`: ACL-Werte/Duplikate kontrolliert; Serializability schützt globale/tenantbezogene Claim-Limits auch zwischen unterschiedlichen Jobs. PostgreSQL-Serializationkonflikte können von EF eingepackt sein: nur bekannte Contention-Codes werden als nicht erworbener Claim behandelt, keine pauschale Fehlerunterdrückung. Lease-Erneuerung, gefenceter Zugriff auf angenommenen Input, unveränderliche geschützte Ergebnisablage und Ergebnislesen für Reconciliation; Erfolgstyp an Jobkind gebunden.
- Snapshots: Größenprüfung vor Kopie, Gesamtlimit, per-Datei-Revision statt falscher Kopplung an Session-CAS-Version, gleiche Revision mit veränderten Bytes abgelehnt; Idle-Frist bei Speicherung verlängert.
- Retention: aktive/unklare Aufträge werden nicht verworfen; abgelaufene Sessions mit aktiven Aufträgen bleiben erhalten. Nach Job-Retention wird nur der Detailinput bereinigt, Idempotenzhash/Receipt bleiben bestehen. Maintenance-Methode ist vorhanden; ein geplanter Host-Cleanup-/Recovery-Runner ist noch nicht angeschlossen.
- `SourceControlWorkspace.cs`: private Root/Owner-Marker, Reparse-Point-Prüfungen, Dateisperre für Quota/Cleanup zwischen Prozessen und volle Clonebudget-Reservierung je Workspace. Quota gilt je konfiguriertem Root/Host; kein verteilter Dateispeicher behauptet. Fremder Actor und aktive Lease blockieren Cleanup.
- `SourceControlHttps.cs`: echte SocketsHttpHandler-Grenze; gemischte öffentliche/private DNS-Antworten werden vollständig abgelehnt. Intern injizierbare DNS-Auflösung dient deterministischen Negativtests, öffentlicher Produktpfad nutzt System-DNS und gepinnte IP-Sockets.
- GitHub-App-Broker fordert bei Push keine PR-Schreibrechte und bei lokalem Commit keine Contents-Schreibrechte an; pro Operation neue Tokenauflösung, keine Cacheumgehung von Rotation/Widerruf.

Pfade oben sind relativ zu `src/VertexBPMN.*`; genaue Dateien stehen im Checkout. Keine Engine-/BPMN-Assertions verändert. Vor Beginn dieses Laufs vorhandene Änderungen an Studio-Bundles wurden nicht angefasst.

## Tatsächliche lokale Abnahme

- Separate Suite `tests/VertexBPMN.SourceControl.Tests`: **55 bestanden, 0 fehlgeschlagen, 0 übersprungen**. Darin 44 Vertragsfälle, vier echte Helper-Prozessfälle, vier echte HTTP-Handler/DNS-Negativfälle, ein Native-Git-Grenzfall, zwei PostgreSQL-Fälle. Kein erfolgreicher Live-GitHub-Tokenexchange/Remote-Fetch daraus ableiten.
- PostgreSQL: vorhandenes `postgres:17-alpine`, isolierter WSLC-Container, ausschließlich Loopback-Port 55439. **Tatsächlicher Container-/Datenbankneustart** zwischen Annahme und erneuter Job-/Idempotenz-/Fencingprüfung bestanden; nicht bloß neuer DbContext. Kein Betriebssystem-Neustart des Vertex-Workers behauptet.
- SQLite-Persistenz gezielt: **9 bestanden**, darunter Lease/Result, ACL-Negativfälle, echte private Windows-Workspace-Quota/Cleanup und konkurrierende Claim-Quota. Der zehnte Retentiontest zusätzlich in der CI-safe Suite bestanden. Kein vollständiger Prozessneustarttest des Vertex-Workers.
- Solution-Build: **0 Fehler, 68 Warnungen**. API-Publish mit Helper erfolgreich. Nach spätesten Codeänderungen Native-Git-Prüfung erneut **1 bestanden**, danach zentrales Testprojekt erneut gebaut: **0 Fehler, 61 Warnungen**; eine Source-Control-Testwarnung `xUnit2031` betrifft den bestehenden Where/Assert.Single-Ausdruck, keine abgeschwächte Assertion.
- Neue isolierte WSLC-Instanz und exakt ihr zugeordnetes anonymes Volume wieder entfernt; keine vorhandenen Images/Daten entfernt.

Alle infrastrukturellen/native Prozessfälle verbleiben im separaten lokalen Testprojekt. `.github/workflows/ci.yml` und zentrale Exclusions unverändert.

Finale CI-safe Suite nach erneutem Build des aktuellen Testprojekts, unveränderte Filter aus `.github/workflows/ci.yml`: **1.382 gesamt, 1.374 bestanden, 0 fehlgeschlagen, 8 bestehende externe Opt-in-Fälle übersprungen**, Exit 0, 85,030 s. Darin alle zehn SQLite-Persistenzfälle und sieben Token-Contractfälle. Neue externe Tests wurden nicht in die zentrale Suite aufgenommen; keine Pflichtexternalabnahme aus Skips ableiten.

Ausführbare lokale Suite (Umgebungsvariablen für isoliertes PostgreSQL, `VERTEXBPMN_TEST_WSLC` mit absolutem Executable-Pfad, `VERTEXBPMN_TEST_POSTGRES_CONTAINER` mit Testcontainer-Präfix und `VERTEXBPMN_TEST_GIT` vorausgesetzt):

```powershell
dotnet test tests/VertexBPMN.SourceControl.Tests/VertexBPMN.SourceControl.Tests.csproj --configuration Release --no-restore --max-parallel-test-modules 1 -p:SkipBpmnIoAssetBuild=true
```

`wslc` ist auf diesem Rechner eine PowerShell-Funktion. Deshalb im Test den tatsächlichen Executable-Pfad konfigurieren, nicht den leeren `Get-Command wslc).Source`-Wert. Keine Zugangsdaten ausgeben. PostgreSQL-Admin-Pooling für den Neustarttest deaktiviert, damit der Cleanup nach Neustart keine vom Broker getötete Verbindung aus dem Pool verwendet.

## Warum Phase 2 noch nicht als vollständig abgenommen gilt

1. SQL-Server-Migration/Upgrade real nicht geprüft; Nutzer hat diese Abnahme ausdrücklich aus dem laufenden Auftrag ausgenommen. Keine weitere Verbindung anfordern oder Infrastruktur bereitstellen. SQLite und PostgreSQL sind nachgewiesen, SQL Server ist nicht automatisch mitbewiesen.
2. Der lokale echte TLS-/Helper-Fetch einschließlich Hooks-/Filter-/Gitlinkfällen und laufendem Timeout/Abbruch ist jetzt nachgewiesen. Linux- und reale GitHub-App-/Remotequalifikation bleiben offen; lokal bestanden heißt nicht live GitHub abgenommen.
3. Maintenance-Host mit Crash-Erkennung/Fencing und sicherem Cleanup sowie typisierte Commit-Annahme sind inzwischen angeschlossen und geprüft. Weitere typisierte Write-Annahmepfade und der externe Effektabgleich fehlen weiterhin. Erwartete Git-IDs/Hashes müssen am G04-Adapter verglichen werden; `ResultUnknown` darf niemals blind erneut schreiben. Die Wartung erkennt Unsicherheit, führt aber keinen Push erneut aus.
4. Spätere Deployment-Provenienz bisher als Vertrags-/geschützte Ergebnisbasis vorhanden, noch keine typisierte Persistenz/Orchestrierung für G08. Keine Deploymentabnahme behaupten.

Nächster Schritt: verbleibende Implementierungs-/Adaptergrenzen schließen. SQL Server bleibt gemäß ausdrücklicher Nutzerentscheidung ungetestet. G02/G03 nicht vollständig markieren, solange die übrigen Pflichtnachweise fehlen; keine ausgesetzte Abnahme als bestanden umdeuten.
