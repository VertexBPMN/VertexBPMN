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

## Abnahmestand

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
