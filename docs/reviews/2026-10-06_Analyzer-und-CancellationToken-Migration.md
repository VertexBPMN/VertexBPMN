# Analyzer-Pakete und CancellationToken-Migration

## Änderungen

Die globalen Analyzer-Pakete in `Directory.Build.props` wurden aktualisiert:

| Paket | Version |
| --- | --- |
| Meziantou.Analyzer | 3.0.294 |
| SonarAnalyzer.CSharp | 10.35.0.4138 |
| Roslynator.Analyzers | 5.0.1 |
| xunit.analyzers | 2.1.0 |

Der Solution-Restore funktioniert wieder. Insbesondere erfüllt xunit.analyzers
jetzt die Mindestversion von xunit.v3.mtp-v2 4.0.1.

## Breaking Change: CancellationToken zuletzt

Auf ausdrücklichen Nutzerwunsch wurden die 15 betroffenen Domain-Methoden statt
einer Legacy-Ausnahme migriert. Schnittstellen, Implementierungen und die
vorhandenen Aufrufer wurden angepasst:

- `IIdentityService`: ListUsersAsync, GetUserByIdAsync, ListGroupsAsync,
  GetGroupByIdAsync, ListAuthorizationsAsync, ListUsersByGroupAsync.
- `IMessageDispatcher`: DispatchServiceTaskAsync, DispatchAiTaskAsync.
- `IOAuth2CredentialFlowService`: StartAuthorizationAsync, CompleteAuthorizationAsync.
- `IRuntimeService`: CorrelateMessageAsync, BroadcastSignalAsync, StartProcessByKeyAsync.
- `ITaskService`: CompleteAsync.
- `IWorkflowTriggerService`: InvokeWebhookAsync.

`CancellationToken` steht jetzt hinter Tenant-, Idempotenz-, Binding- und anderen
optionalen Parametern. Es gibt keine alten Kompatibilitätsüberladungen.
Externe Implementierungen müssen neu kompiliert und entsprechend angepasst werden.
Externe Aufrufer sollten den Token benennen, beispielsweise
`ListUsersAsync(tenantId: tenant, cancellationToken: token)`.
Die fachliche Bedeutung der Parameter und die Tenant-Prüfungen bleiben unverändert.

## Historischer Abnahmestand vom 6. Oktober 2026

- Solution-Restore mit aktualisierten Paketen: erfolgreich.
- Die strikte Analyzer-Konfiguration ist weiterhin aktiv.
- Strikte Builds von Domain, ServiceDefaults, SourceControl.AuthHelper,
  EntityGenerator und MCP-Client: jeweils 0 Warnungen und 0 Fehler.
- Strikter Application-Build: noch 284 Fehler. Dies ist kein erfolgreicher
  Solution-Build; nachgelagerte Projekte bleiben vollständig zu prüfen.
- Aktueller Compiler-Diagnoselauf des Haupttestprojekts: 0 Fehler,
  338 Warnungen bei deaktivierten Analyzern und deaktivierter Warnungseskalation.
- ML-Regression: 4 Tests bestanden. Der echte SDCA-Trainer nutzt einen Thread
  und maximal 100 Iterationen; ein zusätzlicher Test prüft den Trainingspfad
  mit drei historischen Instanzen. Vorher blockierte Thread-Überbelegung den Lauf.
- Erster abgeschlossener CI-sicherer Lauf: 1395 Tests, 1384 bestanden,
  3 fehlgeschlagen, 8 übersprungen. Ursache der drei Fehler: Logger-Mocks
  meldeten Information-Logging als deaktiviert, erwarteten jedoch Logeinträge.
  Die Konfiguration wurde korrigiert; sämtliche Assertions bleiben erhalten.
- Gezielte Handler-Regression danach: 6 Tests bestanden, keine Fehler.
  Die vollständige CI-sichere Wiederholung ist bestanden: 1395 Tests,
  1387 bestanden, 0 fehlgeschlagen, 8 übersprungen; Dauer 1m 22s.
  Infrastrukturabhängige übersprungene Tests sind damit nicht abgenommen.
- Weitere MCP-Ressourcenfreigaben in `McpAgentService.CallAgentAsync` und
  `McpServiceTaskHandler.ExecuteAsync` korrigiert. Neue Tests prüfen freigegebene
  Anfrage-/Antwortinhalte bei erfolgreichem Aufruf und HTTP-Fehlern. Gezielter
  MCP-Lauf: 6 bestanden. Ein unbenutzter privater Parameter der DMN-Tabellen-
  auswertung wurde ohne Änderung der fachlichen Logik entfernt.
- CI-sichere Regression nach diesen Änderungen: 1397 Tests, 1389 bestanden,
  0 fehlgeschlagen, 8 übersprungen; Dauer 1m 26s. Die Tests wurden weiterhin
  aus einem Compiler-Diagnosebuild ohne Analyzer ausgeführt.
- Bestehende Einschränkung außerhalb der geänderten Pfade:
  `McpAgentService.WaitForAgentResponseAsync` erzeugt weiterhin `DemoResponse`;
  dies ist kein Nachweis echter korrelierter MCP-Antwortverarbeitung.
- Vollständiger strikter Solution-Build: weiterhin offen. Die grüne Regression
  verwendet einen Compiler-Diagnosebuild ohne Analyzer und ersetzt diesen nicht.
- Ein separater Compiler-Diagnoselauf mit deaktivierten Analyzern ist ausdrücklich
  kein Nachweis eines erfolgreichen strikten Builds.
- Weitere Analyzer-Funde werden schrittweise behoben; diese Änderung ist noch
  kein Abschluss der gesamten Analyzer-Bereinigung.

## Aktualisierung vom 11. Oktober 2026

Der historische Stand oben ist überholt. Die strikte Release-Kompilierung der
gesamten Solution ist jetzt erfolgreich: **0 Warnungen, 0 Fehler**. Analyzer und
Warnungseskalation bleiben aktiv; der Nachweis stammt nicht aus einem Build mit
deaktivierten Analyzern. Editor-Assets wurden nicht geändert.

```text
dotnet build VertexBPMN.sln --configuration Release --no-restore -p:SkipBpmnIoAssetBuild=true -m:1 --disable-build-servers
```

Nachweis: `solution-strict-final.log`, Dauer 2m 10,74s. Dieser Lauf umfasst auch
API, Studio, Engine, Infrastruktur, AppHost, SDK, CLI, MCP-Projekte, sämtliche
Testprojekte sowie Benchmark- und Test-Runner-Projekte.

### Korrigierte Ursachen der anschließenden Regression

- Die gemeinsame SQLite-Testumgebung verwendet das aktuelle BPMN-Migrationsschema
  und getrennte Datenbanken für zusätzliche EF-Kontexte. Der Sample-Seed verwendet
  bestehende, von den Migrationen angelegte Metadaten idempotent wieder. Betroffen:
  `tests/VertexBPMN.Tests/Infrastructure/SharedSqliteDbFixture.cs` und
  `CustomWebApplicationFactory.cs` im selben Verzeichnis.
- Der SendGrid-Test prüft den tatsächlich serialisierten Versand-Payload der
  aktualisierten Paketversion einschließlich Betreff, Empfänger und Inhalt.
  Betroffen: `tests/VertexBPMN.Tests/Integration/Handlers/SendGridServiceTaskHandlerTests.cs`.
- Negative MCP-Tests berücksichtigen die echte Worker-Registrierung des
  Engine-Konstruktors; fehlende Server-Adressen werden weiterhin abgewiesen.
- C.8.0 verwendet das unveränderte Originalmodell `Vacation Approval.dmn` aus dem
  offiziellen MIWG-Demo-Repository und echte Datums-/Urlaubsguthaben-Eingaben.
  Das vereinfachte Modell mit derselben Entscheidungs-ID wird nicht als Ersatz
  für den abweichenden Originalvertrag verwendet. Quelle und Commit stehen in
  `tests/VertexBPMN.Tests/TestData/Reference/Vacation Approval.source.md`.
- `DmnParser` liest den Namensraum der Modellelemente, nicht einen möglicherweise
  abweichenden XML-Standardnamensraum. Ein zusätzlicher Regressionstest belegt dies.
- `BpmnParser` projiziert Zeebe-Task-Header beider verwendeter Namensräume in die
  Laufzeitattribute. `ProcessEngine` löst deren `decisionRef` auf und bindet das
  lokale Entscheidungsergebnis für namensbasierte FEEL-Ausgabemappings.
- `DmnDecisionGraph` übergibt native `DateTimeOffset`-/`DateTime`-Eingaben mit
  deklarierter DMN-Datumszeit als typisierte FEEL-Zeitwerte statt als gewöhnliche
  JSON-Zeichenketten.

Die ursprünglichen C.8.0-Ausführungsassertionen bleiben erhalten. Zusätzlich werden
die echte DMN-Berechnung (`Approved`), Entscheidungsreferenz, Entscheidungsausführung
ohne `DecisionError` und das echte BPMN-Ausgabemapping geprüft. Keine vorab gesetzten
Ergebnisvariablen, Decision-Service-Mocks, veränderten BPMN-Fixtures oder
Parser-only-Ersatzprüfungen. Gezielter C.8.0-Lauf: **1 bestanden, 0 Fehler**.

### Abschließende Testläufe

| Lauf | Gesamt | Bestanden | Fehler | Übersprungen | Dauer |
| --- | ---: | ---: | ---: | ---: | --- |
| Hauptsuite mit unveränderten CI-sicheren Ausschlüssen | 1457 | 1449 | 0 | 8 | 1m 26,672s |
| Dienstunabhängige SourceControl-Suite | 105 | 105 | 0 | 0 | 3,475s |
| C.8.0 mit originalem externem DMN-Modell | 1 | 1 | 0 | 0 | 4,799s |

Die gezielte C.8.0-Prüfung ist außerdem Bestandteil der Hauptsuite, keine zusätzliche
einzigartige Testanzahl. Nachweise: `main-tests-strict-final-results.log`,
`sourcecontrol-tests-strict-final-results.log`, `c8-original-test.log`.
Alle Läufe verwenden die erfolgreichen aktuellen strikten Release-Builds mit
`--no-build --no-restore`. Die acht übersprungenen Fälle benötigen externe lokale
Dienste oder Live-Zugangsdaten und gelten ausdrücklich nicht als bestanden.

Die separate SourceControl-Prüfung schließt nur diese lokalen Abnahmeklassen aus:
`PostgresAcceptanceTests`, `NativeGitBoundaryTests`, `GitHttpsTransportTests`,
`CredentialChannelAcceptanceTests`, `CommitExecutionAcceptanceTests` und
`KeycloakGitLocalAcceptanceTests`. Deren Kompilierung ist nachgewiesen, ihre erneute
Laufzeitabnahme nicht.

### Gezielte Clean-Code-Nachprüfung

Scope der letzten Nachprüfung: `C_8_0_Test.cs`, `DmnParserTests.cs`,
`DmnParser.cs`, `BpmnXmlNamespaces.cs` (vier C#-Dateien). D1–D7-Suchen und
manuelle Prüfung der geänderten Logik wurden ausgeführt. Keine bestätigten neuen
Clean-Code-Funde in diesem Scope. Dies ist keine vollständige manuelle Prüfung
aller schon zuvor vorhandenen Änderungen des umfangreichen Worktrees.

| Regel | Ergebnis / Begrenzung |
| --- | --- |
| Async-Verträge und Cancellation | PASS; I/O propagiert den Test-Token. Die bestehende synchrone Engine-Überladung mit Eingabevariablen hat eine begründete CA1849-Einzelausnahme. |
| Collection-Verträge | PASS; keine neuen unklaren öffentlichen Collection-Verträge. |
| Bool-Modusschalter | PASS; keine neuen Modusschalter. |
| Verantwortungsnamen | PASS; keine neuen Sammeltypen. |
| Regions und Struktur | PASS; keine neuen Regions. |
| Interfaces und Grenzen | N/A für neue Verträge; bestehendes IDmnParser bleibt unverändert erhalten. |
| Vererbung | N/A für neue Hierarchien; keine neue Klassenvererbung. |
| Lesbarkeit | PASS im genannten Scope; Assertions sind echte Berechnungs-/Ausführungsprüfungen. Kein repositoryweiter Formatter-Lauf. |

### Noch nicht erneut abgenommen

Der Build belegt Kompilierbarkeit mit der aktuellen strikten Konfiguration, nicht
Produktionsreife sämtlicher Integrationen. PostgreSQL-/Keycloak-/WSLC-, reale
Git-Transport-, Browser-, Ausfall-, Wiederherstellungs-, Last-/Soak- und
Cloud-Abnahmen wurden in dieser Bereinigung nicht erneut durchgeführt.
Die CI-sichere Regression verwendet unverändert die Ausschlüsse aus
`.github/workflows/ci.yml`; keine weiteren Fälle wurden zum Erzielen eines grünen
Ergebnisses ausgeschlossen. Der bestehende MCP-Demo-Antwortpfad bleibt die oben
dokumentierte, separate Funktionsgrenze.
