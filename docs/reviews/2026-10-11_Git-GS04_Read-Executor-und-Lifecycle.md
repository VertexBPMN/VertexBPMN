# GS04: privater HTTPS-Read-Executor und Auftragslebenszyklus

Stand: 11. Oktober 2026. Branch `codex/git-generic-servers`, Basis `02ebe35`, lokale Änderungen. Fortsetzung von [Read-Claiming und Transport](2026-10-11_Git-GS04_Profilgebundenes-Read-Claiming-und-Transport.md).

Der interne Readpfad verbindet jetzt den bestehenden profilgebundenen Fetch mit einem signierten privaten Workspace, aktuellen Berechtigungsprüfungen, Leaseheartbeat, gefencetem Abschluss und Cleanup. API, Studio und Jobhost rufen diesen neuen Runner noch nicht auf. GS04 insgesamt und allgemeiner Git-Server-Support bleiben offen.

## Geänderte Dateien

| Datei | Verantwortung |
| --- | --- |
| `src/VertexBPMN.Infrastructure/SourceControl/SourceControlHttpsReadExecutor.cs` | Aktuellen Auftrag lesen, eigenen Workspace erstellen, bestehenden Fetch ausführen und vertrauenswürdige interne Leseprojektion vor/nach Ausführung gegen aktuelle Rechte prüfen |
| `src/VertexBPMN.Infrastructure/SourceControl/SourceControlClaimedReadRunner.cs` | Actor/Lease/Fence am Einstieg, frische Scopes für Heartbeat und Abschluss, gesamte Lesezeit begrenzen, Fehler klassifizieren und Cleanup versuchen |
| `src/VertexBPMN.Infrastructure/SourceControl/PersistentSourceControlStore.HttpsReadCompletion.cs` | Erfolgsabschluss mit erneuter Kontextprüfung und gefencetem Update in einer Serializable-Transaktion |
| `src/VertexBPMN.Infrastructure/SourceControl/ClaimedReadOutcome.cs` | Erfolg beziehungsweise sicherer Fehler und getrennte Anzeige eines ausstehenden Cleanup |
| `src/VertexBPMN.Infrastructure/InfrastructureModule.cs` | Beide internen Dienste registrieren; kein Aufruf durch aktiven Produktprovider |
| `tests/VertexBPMN.Tests/Unit/Application/SourceControlConnectionBindingTests.cs` | Workspace-Testinstanz mit derselben isolierten Schutzgrenze erstellen |
| `tests/VertexBPMN.Tests/Unit/Application/SourceControlConnectionBindingTests.HttpsReadRunner.cs` | Elf lokale Lebenszyklusfälle mit kontrollierter Fetch-/Projektionsfunktion |

## Verhalten und Grenzen

1. Der Runner akzeptiert nur den aktuellen Actor eines laufenden Version-2-Readauftrags mit passender Workeridentität, Fence und gültiger Lease. Nicht nachgewiesene Ownership erlaubt keine Zustandsänderung; unerwartete Backenddiagnostik wird sicher klassifiziert.
2. Vor Workspaceerstellung und Fetch werden aktuelle Rechte und der geschützte ursprüngliche Auftrag geprüft. Die produktive Executor-Methode ruft ausschließlich `FetchClaimedRevisionReadAsync` auf; Ziel und vollständige Commit-ID stammen aus dem angenommenen Auftrag.
3. Heartbeat verwendet einen eigenen Datenbankkontext und löst Rollen sowie Profil-/Policykontext erneut auf. Verlust der Lease oder dieser Voraussetzungen bricht die laufende Arbeit ab. `ReadTimeout` umfasst Fetch und anschließende Projektion; Caller-Abbruch und internes Timeout erhalten unterschiedliche Fehlercodes.
4. Der Erfolg wird erst nach Ende des Heartbeats und erneuter Autorisierung/Leaseprüfung in einer frischen Serializable-Transaktion bestätigt. Das Update bindet weiterhin Worker/Fence und aktuelle Lease. Ein altes Ergebnis wird bei einer fremden Übernahme nicht ausgeliefert.
5. Fehlerabschluss verwendet einen frischen Scope und unabhängigen, begrenzten Token, auch wenn die ausführende Arbeit abgebrochen wurde. Unerwartete Exceptiontexte werden weder als Ergebnis noch als Fehlercode übernommen. Nicht bestätigbarer Abschluss führt zu `ResultUnknown` ohne Wert.
6. Cleanup verwendet einen frischen Scope und die bestehende signierte Workspacegrenze. Eine aktive Ersatzlease oder ein fremder/ungültiger Eigentumsmarker verhindert das Löschen. `CleanupPending` meldet den ausstehenden Versuch, ohne einen schon bestätigten Erfolg zu überschreiben. Der Flag ist derzeit Bestandteil des internen Rückgabewerts, kein neuer dauerhafter Cleanupdatensatz.
7. Der Executor kennzeichnet einen Workspace erst nach vollständig erfolgreichem `CreateAsync` als erstellt. Ein möglicherweise unvollständiges Verzeichnis ohne bestätigten Eigentumsmarker wird nicht auf Verdacht gelöscht.

Die Projektion ist eine interne vertrauenswürdige Funktion, kein HTTP-Request und keine bereits implementierte File-/History-/Diff-API. Der Zustand des Erfolgs ist dauerhaft gespeichert; der generische Projektionswert wird nur intern zurückgegeben und noch nicht als abrufbares Read-Ergebnis persistiert. Wiederanlauf und Ergebnisauslieferung nach Prozessabbruch sind deshalb weiterhin Pflichtarbeit.

## Lokale Prüfungen

| Prüfung | Ergebnis |
| --- | --- |
| Strenger Release-Gesamtbuild, ohne Restore und mit unveränderten Editorassets | 0 Warnungen, 0 Fehler; 33,51 Sekunden |
| Gezielte Git-/Profil-/Autorisierungsregressionen einschließlich elf neuer Lebenszyklusfälle | 327 bestanden, keine Fehler/Skips; 29,592 Sekunden |
| Bestehende `LeaseRunnerTests` | 4 bestanden, keine Fehler/Skips; 0,602 Sekunden |
| CI-safe Hauptsuite mit unveränderten Workflow-Ausschlüssen | 1.718 bestanden, 0 fehlgeschlagen, 8 externe Skips, insgesamt 1.726; 1 Minute 37,746 Sekunden |
| Check-only Whitespace der vier neuen Produktdateien und zwei Testdateien | Exit 0; DI-Registrierungen separat im Diff gelesen |
| Scoped Clean-Code-Review | Sieben C#-Dateien; D1–D7 ohne Suchfehler, D8 manuell gelesen |
| `git diff --check` | Exit 0; nur bestehende LF-/CRLF-Hinweise in zwei früher geänderten Dateien |

Logs: `artifacts/git-generic-servers/gs04-read-runner-*`. Der zuvor sporadisch beobachtete C8-FEEL-Fehler trat in diesem CI-Lauf nicht auf; seine Ursache ist weiterhin ungeklärt.

```powershell
dotnet build VertexBPMN.sln --configuration Release --no-restore -p:SkipBpmnIoAssetBuild=true -m:1 --disable-build-servers
```

Die gezielte Haupttestauswahl umfasst `GitHttpsPolicyRegistryTests`, `GitHttpsNetworkBoundaryTests`, `SourceControlConnectionBindingTests`, `SourceControlConnectionProfilePersistenceTests`, `RepositoryConnectionProfileTests`, `SourceControlExecutionTargetTests`, `LegacyGitHubConnectionMappingTests`, `SourceControlSecurityTests`, `SourceControlOptionsTests` und `SharedSqliteDbFixtureIsolationTests`. Die bestehende Leaseauswahl wurde über die gebaute `VertexBPMN.SourceControl.Tests.dll` ausgeführt. Native-Git-/TLS-Tests wurden in dieser Fortsetzung nicht erneut ausgeführt, da ihre Implementierung unverändert blieb; ihr vorheriger separater Nachweis steht im [vorigen Bericht](2026-10-11_Git-GS04_Profilgebundenes-Read-Claiming-und-Transport.md).

Für die Hauptsuite wurde exakt der Schritt **Test (CI-safe suite)** aus `.github/workflows/ci.yml` mit `--configuration Release --no-build --no-restore`, allen dort festgelegten Kategorie-/Klassen-Ausschlüssen und `--max-parallel-test-modules 1` verwendet. Die acht Skips betreffen fehlende Voraussetzungen für Azure Data Protection, PostgreSQL und Live-TypeSafe; diese Abnahmen gelten nicht als bestanden.

Die Tests ersetzen den Fetch durch eine kontrollierte interne Funktion. Sie prüfen private Workspaceerstellung, Erfolg, Fehlerklassifikation, Caller-Abbruch, Timeout bei Fetch und Projektion, verlorenen Fence, Widerruf nach Fetch, frische Abschlussautorisierung, Heartbeat-Abbruch, fremden Actor und manipulierten Workspace-Eigentumsmarker. Die Scopes besitzen unterschiedliche EF-Kontexte auf derselben isolierten SQLite-Verbindung. Das belegt keine getrennten DB-Verbindungen/Prozesse und keinen kompletten DB-/Native-Git-/TLS-Ablauf.

## Clean-Code-Review

Verwendeter Skill: `.agents/skills/dotnet-clean-code-review/SKILL.md`. Scope sieben C#-Dateien dieser Fortsetzung; neue Dateien vollständig, vorhandene Dateien auf die aktuellen Änderungen und betroffenen Aufrufer begrenzt. Basis `02ebe35`; generierte Dateien ausgeschlossen. D1–D7 liefen ohne Suchfehler, Treffer wurden gelesen; D8 umfasst alle zwölf Lesbarkeitskategorien. Breite Treffer in bestehenden Dateien sind Kontext und keine neuen Befunde.

| Regel | Ergebnis / Evidenz |
| --- | --- |
| 1 Async/Cancellation | PASS: awaitbare Async-Verträge, Token für DB, Workspace, Fetch und Projektion; unabhängiger begrenzter Abschluss/Cleanup; keine neuen async-void-Verträge |
| 2 Collection-Verträge | PASS: Rollen vor Await kopiert; keine neue Collection-Rückgabe mit unklaren Lazy-/Streamsemantiken |
| 3 Boolean-Modi | PASS: Ownership-/Cleanupzustand als Daten; keine neuen bool-Modusparameter |
| 4 Verantwortungsnamen | PASS: Executor, Runner, Ergebnis und Persistence benennen ihre Aufgaben |
| 5 Regions | PASS: keine neuen Regions |
| 6 Interfaces/Boundaries | PASS: bestehende Actor-/Options-/Scope-Grenzen, keine neuen Interfaces |
| 7 Vererbung/Komposition | PASS: direkte Komposition und bestehende Partialklasse; keine neue Klassenhierarchie |
| 8 Lesbarkeit | PASS: Guards, benannte Lebenszyklusschritte und expliziter Cleanupstatus; eigene Analyzerbefunde behoben |

Keine bestätigten verbleibenden Clean-Code-Befunde in diesem Scope. Frühere lokale Builds meldeten leere Catch-Blöcke, eine fehlende EF-Extension-Importanweisung und fehlende explizite Testtoken beziehungsweise deren Parameterreihenfolge; behoben ohne Analyzerunterdrückung oder abgeschwächte Assertions.

## Nächste Arbeit und offene Pflichtabnahmen

- Typisierte File/List-, History- und Diff-Ergebnisse mit sicherer dauerhaft abrufbarer Auslieferung ergänzen; Branchauflösung separat gegen feste Commit-IDs binden.
- Den vollständigen Pfad aus Annahme, Claim, echtem Native-Git-Fetch, Modelllesen, Abschluss und Cleanup gegen einen isolierten eigenen HTTPS-Server mit eigener CA prüfen. Kein solcher kombinierter Servernachweis in dieser Fortsetzung.
- Prozessabbruch, persistente Schlüssel, getrennte DB-Verbindungen, konkurrierende Worker und Wiederaufnahme mit neuem Fence abnehmen; unvollständige Workspaceerstellung und Cleanupwartung berücksichtigen.
- API/Studio und Jobhost erst mit passenden Typverträgen und Fehler-/Capabilityverhalten anbinden. Generische Commit-/Push-/Unknown-Abläufe sowie Hosting/Azure/SSH nach ihren Planabhängigkeiten liefern.

Kein Commit, Push, Deployment, externer Testwrite oder Benutzerdatenbankmigration ausgeführt. Website und Wiki erhalten keine unbelegte allgemeine Supportfreigabe.
