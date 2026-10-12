# Git GS04: Profilgebundenes Read-Claiming und Transport

Stand: 11. Oktober 2026. Branch `codex/git-generic-servers`, Basis `02ebe35`, lokale Änderungen. Fortsetzung der [HTTPS-Auftragsannahme und Rekonstruktion](2026-10-11_Git-GS02_HTTPS-Auftragsannahme-und-Wiederaufnahme.md) gemäß [generischem Git-Plan](2026-10-09_Git-Generische-Server-Unterstuetzung_Implementierungsplan.md).

## Ergebnis und Status

Der geschützte Version-2-Kontext kann jetzt über einen getrennten internen Claimpfad übernommen werden. Ausführbar sind ausschließlich typisierte private Leseaufträge mit einer vollständigen SHA-1-/SHA-256-Commit-ID. Der Auftrag enthält seine eigene verpflichtende Request-Schemaversion. Freie Revisionsausdrücke, fehlende Felder, unbekannte Versionen und rohe Read-Payloads werden vor der Claimänderung abgelehnt.

Der bestehende Legacy-Claimpfad und der Hintergrundworker bleiben unverändert auf Version 0 begrenzt. Der neue interne Transport verbindet den aktuellen Claim mit dem gespeicherten Profil-/Netzwerk-/CA-Kontext, frischer Rollenauflösung, einmaliger DNS-Freigabe, dem Profilcredentialbroker und dem kontrollierten nativen Fetch. Der ausführende Aufruf erhält keine ersetzbare Commit-ID; diese stammt ausschließlich aus dem akzeptierten Request.

**Noch kein aktiver API-/Studio-/Produktproviderpfad.** Es gibt noch keinen neuen Hintergrundrunner, der Readjobs auswählt, Leases während einer längeren Ausführung erneuert, Ergebnisse ausliefert und Ressourcen abschließend bereinigt. Die native Fetchorchestrierung ist implementiert; ihr vollständiger kombinierter Ablauf mit DB-Claim, echtem privaten HTTPS-Server und Prozessneustart wurde in dieser Fortsetzung nicht abgenommen. Lokale Tests prüfen DB-Claims und Transportvorbereitung getrennt von den bestehenden nativen HTTPS-Regressionen. GS02 und GS04 insgesamt bleiben offen.

## Dateien

| Datei unter `src/VertexBPMN.Infrastructure/SourceControl/` | Änderung |
| --- | --- |
| `AcceptedGitRevisionRead.cs` | Typisierter Request mit verpflichtender Commit-ID und Schemaversion |
| `AcceptedGitRevisionReadWork.cs`, `PreparedGitRevisionRead.cs` | Ausführungskontext und Commit-ID, anschließend zugehörige DNS-Freigabe |
| `PersistentSourceControlStore.HttpsReadClaims.cs` | Typisierte Annahme, gesondertes Claiming, aktuelle Lease-/Fenceprüfung und Read-Recovery |
| `PersistentSourceControlStore.HttpsExecution.cs` | Validierten DB-Datensatz und entschlüsselten Kontext gemeinsam zurückgeben; Requestdecoder verwendet genau diesen validierten Datensatz |
| `ProfiledGitHttpsTransport.ClaimedReads.cs` | Claimgebundene Vorbereitung und Fetchorchestrierung mit frischer Autorisierung vor/nach den I/O-Stufen |
| `ProfiledGitHttpsTransport.cs` | Partialklasse für die zusätzliche Orchestrierung |

Tests: `tests/VertexBPMN.Tests/Unit/Application/SourceControlConnectionBindingTests.HttpsReadClaims.cs` enthält 31 neue Fälle; die bestehende Fixture in `SourceControlConnectionBindingTests.cs` erhält ausschließlich eine optionale Optionsübergabe für Quotentests. Insgesamt neun C#-Dateien in dieser Fortsetzung; keine neue Migration, Abhängigkeit, Konfigurationsoption oder Änderung an nativen Git-/TLS-Schutzregeln.

## Claim- und Ausführungsvertrag

1. Aktuelle Rechte, geschützter Zielkontext, Requesthash und administrativer Snapshot werden vor Claimänderung geprüft. Die vollständige Commit-ID wird aus dem bereits validierten Requestdatensatz rekonstruiert. Ein maximal 2.048 Zeichen langer geschützter Revisionsrequest verhindert, dass große generische Read-Payloads als dieser kleine Requesttyp ausgeführt werden.
2. Serializable-Transaktion, globale und mandantenbezogene bestehende Jobquoten sowie ein vergleichender DB-Update schützen die Übernahme. Das Update bindet Actor, Auftragstyp, Version, Payloads, Hash, Zustand, Fence und abgelaufene/fehlende Lease.
3. Eine laufende, nicht abgelaufene Lease verhindert eine zweite Übernahme. Erneute private Reads nach abgelaufener Lease beziehungsweise Unknown/Reconciling erhöhen den Fence und laufen als Read erneut. Diese Wiederholung gilt ausschließlich für private Fetches und erweitert keine Write-Reconciliation.
4. Alte Worker können über die bestehenden gefenceten Finish-/Leaseoperationen keine Belege unter dem neuen Fence bestätigen. Erfolgreich abgeschlossene Readjobs werden nicht wieder beansprucht.
5. Vor DNS prüft der Transport die aktuelle Lease und den ursprünglichen Profil-/Netzwerk-/Trustkontext; nach DNS erfolgt dieselbe Prüfung erneut. Der festgehaltene administrative Snapshot wird nicht durch die aktuelle Registry ersetzt.
6. Vor Credentialauflösung, nach deren Abschluss und vor/nach Git wird der aktuelle Auftrag erneut geprüft. Git erhält die einmal freigegebenen Adressen, das ursprüngliche öffentliche CA-Bündel und genau die gespeicherte Commit-ID. Ein ReadTimeout umfasst die Fetchorchestrierung; Caller-Cancellation bleibt erhalten, internes Timeout wird sicher klassifiziert.
7. Ein Workspace mit fremder Auftrags-ID oder fremdem Fence wird bereits am Einstieg abgewiesen. Der künftige Executor muss ihn weiterhin über `SourceControlWorkspace.CreateAsync` erzeugen; dieser Teilpfad ersetzt keine Prüfung der signierten Workspacewurzel. Der Fetch führt keinen automatischen erfolgreichen Jobabschluss aus, da die anschließende Modelllese-/Ergebnisauslieferung noch folgen muss.

Keine Secrets oder aufgelösten DNS-Adressen werden dauerhaft gespeichert. Der direkte Native-Git-Aufruf bleibt intern; weder Browserparameter noch Caller-Revisionsausdrücke bestimmen sein Ziel.

## Lokale Prüfungen

| Prüfung | Ergebnis |
| --- | --- |
| Strenger Gesamtbuild, Release ohne Restore, unveränderte Editorassets | 0 Warnungen, 0 Fehler; 29,37 Sekunden |
| Gezielte Git-/Sicherheitsauswahl | 316 bestanden, 0 Fehler/Skips; 22,955 Sekunden |
| Native Git-/HTTPS-/Lease-/Credential-Regressionen | 70 bestanden, 0 Fehler/Skips; 251,391 Sekunden |
| CI-safe Hauptsuite mit unveränderten CI-Ausschlüssen | 1.707 bestanden, 0 fehlgeschlagen, 8 externe Skips, insgesamt 1.715; 1 Minute 53,312 Sekunden |
| Check-only Whitespace, neun C#-Dateien | Exit 0 |
| Scoped Clean-Code-Prüfung | D1–D7 ohne Suchfehler, geänderte Mitglieder/neue Dateien manuell gegen D8 geprüft |

Logs: `artifacts/git-generic-servers/gs04-claimed-reads-*`. Die frühen Builds meldeten Dateinamenabweichungen für zwei neue Records und eine verschachtelte Ternärform im neuen Test; durch eigene Dateien und eine lesbare Switchzuordnung behoben. Keine Testassertion abgeschwächt, keine Fixture zur Umgehung von Produktfehlern geändert und keine externen Checks als bestanden markiert. Der zuvor beobachtete sporadische C8-FEEL-Fehler trat im aktuellen CI-Lauf nicht auf; seine Ursache bleibt ungeklärt.

```powershell
dotnet build VertexBPMN.sln --configuration Release --no-restore -p:SkipBpmnIoAssetBuild=true -m:1 --disable-build-servers
```

Die gezielte Haupttestauswahl ist identisch zum [vorigen Bericht](2026-10-11_Git-GS02_HTTPS-Auftragsannahme-und-Wiederaufnahme.md). Native Auswahl: `GitHttpsTransportTests`, `LeaseRunnerTests`, `NativeGitBoundaryTests`, `BasicCredentialChannelTests` und `CredentialChannelAcceptanceTests` mit lokalem `VERTEXBPMN_TEST_GIT`. CI-Auswahl: Schritt **Test (CI-safe suite)** in `.github/workflows/ci.yml` mit sämtlichen dort festgelegten Ausschlüssen und `--configuration Release --no-build --no-restore`.

Die neuen Fälle belegen unter anderem eine neue EF-/Storeinstanz auf derselben isolierten SQLite-Datenbank, konstante akzeptierte Commit-ID, Ablauf/Übernahme mit höherem Fence, Abweisung alter Worker, gemeinsame Quoten, Rollen-/Grant-/Profil-/Netzwerkänderung vor Claim und vor/während DNS, Abbruch, unbekannte Requestversionen, unzulässige Revisionsausdrücke sowie fremde Workspace-Identitäten. Das ist kein Hostneustart mit persistenten Schlüsseln und kein kompletter neuer Native-Git-Executor-Acceptance-Lauf.

## Clean-Code-Review

Skill `.agents/skills/dotnet-clean-code-review/SKILL.md`; Scope neun C#-Dateien, Basis `02ebe35` und lokale Änderungen. Neue Dateien vollständig, vorhandene Dateien nur aktuelle Änderungen und betroffene Aufrufer. Generierte Dateien ausgeschlossen. Suchkandidaten vollständig in `gs04-claimed-reads-clean-code.log`; D1–D7 ohne Fehler und ohne abgeschnittene Ergebnisse. D8: alle zwölf Lesbarkeitskategorien manuell gelesen.

| Regel | Ergebnis / Evidenz |
| --- | --- |
| 1 Async/Cancellation | PASS: awaitbare Async-Verträge, Token für DB/DNS/Rollen/Credentials/Git, kein async void; Caller-Cancellation bleibt erhalten |
| 2 Collection-Verträge | PASS: Rollen vor Await kopiert; begrenzte administrative/DNS-Snapshots; keine neue verzögerte Collection-Rückgabe |
| 3 Boolean-Modi | PASS: benannte Claim-/Slot-Prädikate; keine neuen bool-Modusschalter |
| 4 Verantwortungsnamen | PASS: Request, akzeptierte Arbeit, Vorbereitung, Persistence und Transport getrennt |
| 5 Regions | PASS: keine neuen Regions |
| 6 Interfaces/Boundaries | PASS: vorhandene Actor-, Options- und Persistence-Grenzen; keine neuen Interfaces |
| 7 Vererbung/Komposition | PASS: vorhandene Partialklassen ergänzt, keine neue Vererbungshierarchie |
| 8 Lesbarkeit | PASS: Guards, kleine Schritte, benannte Zustandsprädikate, kohärenter validierter Datensatz; bestätigte neue Analyzerbefunde behoben |

Keine bestätigten verbleibenden Clean-Code-Befunde in diesem Scope. Ein grüner Review beweist keine externe Transportabnahme.

## Nächste Arbeit und Pflichtabnahmen

1. Claim-/Fetchpfad mit einem echten Executor aus `SourceControlWorkspace`, Leaseheartbeat, Modellleseoperation, gefencetem Abschluss und sicherem Cleanup verbinden; fehlgeschlagene DB-Transaktionen über frische Scopes abschließen.
2. Branchauflösung, File/List, History und Diff als typisierte dauerhafte Readoperationen ergänzen und API/Studio inkrementell anbinden.
3. Gleichzeitige echte Claims mit getrennten DB-Verbindungen/Prozessen, persistente Schlüssel, Crash, Rollen-/Profilwiderruf während Credential-/Git-I/O und komplette Restart-Recovery gesondert prüfen.
4. Generische Commit-/Ref-CAS-Push- und Unknown-Abläufe sowie Hosting/Azure/SSH gemäß Plan. Alte Binärversionen vor Aktivierung neuer Jobtypen drainen/stoppen; aktuelle Filter ändern das Verhalten bereits laufender alter Worker nicht.
5. Echte eigene HTTPS-Server mit eigener CA und freigegebenem Port durch API/Studio abnehmen. Website/Wiki erst um den tatsächlich belegten Support erweitern.

Kein Commit, Push, Deployment, externer Testwrite oder Benutzerdatenbankmigration ausgeführt.
