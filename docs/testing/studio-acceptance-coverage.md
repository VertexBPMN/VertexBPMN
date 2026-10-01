# Studio: Use-Case-Abdeckung und Nachvollziehbarkeit

Stand: 2026-10-01. Referenzbasis: `master` bei `68370c9`;
Ergänzungen auf `codex/studio-use-case-traceability`.

## Verbindliche Abdeckungsmatrix

Der versionierte [Use-Case-Katalog](studio-use-cases.tsv) ist die maschinenlesbare
Matrix. Er überführt die zehn Use Cases und die Varianten der Abschnitte 2.1–2.11
aus `VertexBPMN-Studio-BPMN-Editor-Testplan.md` (ursprünglich im Downloads-Ordner)
in das Repository und ergänzt Administration, Navigation, Fehlerpfade und OIDC.
Damit benötigt die Zuordnung nicht mehr die private Downloads-Datei.
Die fachlichen Szenarien bleiben Anforderungen, nicht behauptete Produktergebnisse.

Jede Zeile nennt eine stabile ID, Szenario, Testart, Implementierungsstatus,
Quelldatei, Testmethode, gegebenenfalls Theorie-Variante und verbleibende Lücke.
Zusätzliche `INV-*`-Zeilen inventarisieren bisher nicht primär zugeordnete Tests.
Diese sind ausdrücklich keine zusätzlichen Belege manueller Anforderungen.

| Kennzahl | Statisch ermittelter Stand |
|---|---:|
| Manuelle und ergänzte Acceptance-Anforderungen | 128 |
| Anforderungen mit vorhandener primärer Testzuordnung | 68 |
| Anforderungen mit neu ergänzter primärer Testzuordnung | 16 |
| Anforderungen ohne implementierte primäre Testzuordnung | 44 |
| Zugeordnete Anforderungen mit weiterem fehlendem Teilablauf | 55 |
| Zusätzlich inventarisierte Bestandsmethoden | 57 |
| Sämtliche zugeordneten Testmethoden, inklusive Theorien | 115 |
| Nicht zugeordnete Testmethoden | 0 |

16 neue Zuordnungen bedeuten **nicht** 16 neue Tests: mehrere Anforderungen
verweisen auf denselben Ablauf. Ergänzt wurden **fünf Methoden mit zehn konkreten
Testfällen**. Eine Theorie zählt als eine Methode, aber mehrere ausführbare Fälle.
Es wurden in diesem Arbeitspaket **keine Tests ausgeführt**.

### Bedeutung der Kennzeichnungen

- `existing`: Testcode vorhanden. Kein aktueller Ausführungs- oder Abnahmebeleg.
- `added`: Neu implementiert und kompiliert, **nicht ausgeführt**.
- `gap`: Kein primär zugeordneter Acceptance-Test implementiert. Das bedeutet nicht,
  dass die Produktfunktion zwingend fehlt oder keine andere Unit-Prüfung existiert.
- Ein nicht leeres `Remaining` bedeutet, dass der zugeordnete Test die Anforderung
  noch nicht vollständig abdeckt, unabhängig davon, ob der Test irgendwann grün war.
  `-` im TSV bedeutet keine weitere dokumentierte Teil-Lücke; der Prüfer gibt
  dies als leeres Feld aus, nicht als Ausführungs- oder Abnahmeerfolg.
- `browser-real`: Playwright gegen echtes Studio/API, mit unabhängigen
  Persistenz-/History-Assertions; Import ist trotzdem kein grafisches Neuzeichnen.
- `api-real`: echter API-/Engine-Test, **kein GUI-Test**.
- `browser-smoke`: sichtbare Seite/Heading/Reload, kein vollständiger Action-Workflow.
- `browser-stub`: Browserprüfung mit isolierter Seite oder Stub-API;
  kein Beleg einer echten Backend-Persistenz oder Runtime.
- `browser-oidc`: separates lokales Keycloak-Profil, nicht Teil des normalen
  `LocalStudioE2E`-Runners.
- `inventory`: eindeutiger Codeverweis eines Bestandsfalls; Testart und Assertions
  müssen im Quellcode gelesen werden. Zählt nicht als zusätzliche Fachabdeckung.

## Neu implementierte echte Browserabläufe

Quelle: [LocalStudioInfrastructureTests.TraceableWorkflows.cs](../../tests/VertexBPMN.Studio.UiTests/LocalStudioInfrastructureTests.TraceableWorkflows.cs).

| IDs / Varianten | Konkrete Assertions | Verbleibender Teil |
|---|---|---|
| UC02-HIGH/LOW; 5000/10 | Import → sichtbares Diagramm → tatsächlicher XML-Download → semantischer Reimport → Validate → UI-Deploy → Repository; nur gewählten Inbox-Task claimen/abschließen; richtige History-Flow-ID; anderer Pfad nie ausgeführt | Condition/Default werden importiert, nicht im Properties Panel konfiguriert; Start per API |
| UC03; parallel | Zwei Aufgaben gleichzeitig; GUI-Claim/Completion je Aufgabe; kein End-/Completed-Zustand nach nur einer Aufgabe; danach echte Completion | Import statt grafischem Zeichnen; Start per API |
| M2.6-INCLUSIVE; inclusive | Genau zwei zutreffende Aufgaben; beide über GUI abschließen; kein vorzeitiger Join; dritter Pfad nie ausgeführt | Import statt grafischem Zeichnen; Start per API |
| UC09/M2.9-PARALLEL; multi-parallel | Collection Alice/Bob/Carol als lokale Elementvariablen; drei verschiedene Aufgaben; GUI-Completions; kein vorzeitiges Ende | MI-Konfiguration importiert; Start per API |
| M2.9-SEQUENTIAL; multi-sequential | Stets genau eine offene Iteration; drei verschiedene Aufgaben und GUI-Completions; Ende erst nach letzter Iteration | MI-Konfiguration importiert; Start per API |
| UC08-EMBEDDED; embedded | Expanded Subprocess wird angezeigt und roundgetrippt; innere Aufgabe in echter Inbox; Completion und persistierte History | Drilldown/up und Neuanlage fehlen; Start per API |
| UC08-CALL | Child und Parent im Modeler deployen; separate Child-ID mit Parent-ID/CallingActivityId; Parent wartet am Call; nach Child-GUI-Completion echte Parent-Fortsetzung und Completion | calledElement importiert; Start per API |
| UC10-PIN | v1-Instanz wartet; v2 im Modeler deployen; unterschiedliche Definition-IDs; alte Instanz behält v1-Aufgabe; neue hat v2-Aufgabe; beide GUI-Completions; kein v2-Element in v1-History | Start per API |
| ADMIN-CSV | Tatsächlicher CSV-Browserdownload; reale Completed-Instanz; richtiger Header und eine Datenzeile; anderer Tenant und anderer Prozess ausgeschlossen; Fehlermeldung gilt nicht als Erfolg | Modelltraining ist ein eigener offener Fall |

Die neuen Browserfixtures ergänzen ausschließlich Diagram Interchange (DI) zu
eigenen Testmodellen. Bestehende gültige BPMN-Konformitätsfixtures und Engine-
Assertions werden nicht angepasst, abgeschwächt oder durch Parserprüfungen ersetzt.

## Statische Prüfung und lokale Ausführung

Die Zuordnung lässt sich **ohne Build, Browser, Services oder Testausführung** prüfen:

```powershell
./scripts/check-studio-use-case-coverage.ps1 -ListAllTests
./scripts/check-studio-use-case-coverage.ps1 -UseCase UC03,UC09,ADMIN-CSV
./scripts/check-studio-use-case-coverage.ps1 -AsJson
```

Der Prüfer kontrolliert IDs, Status, Dateipfade und tatsächlich deklarierte
Methoden. Er inventarisiert auch nicht zugeordnete neue Methoden. Er prüft nicht,
ob ein Selector zur Laufzeit funktioniert oder die Assertion bestanden wurde.
`-RequireComplete` ist ein strenger Vollständigkeitscheck und **muss derzeit
fehlschlagen**, weil `gap`- und `Remaining`-Anforderungen offen sind.

Nur auf ausdrücklichen Wunsch später ausführen, nicht in diesem Arbeitspaket:

```powershell
./scripts/test-studio-e2e.ps1 -Infrastructure Wslc -TestMethod `
  BrowserWorkflow_ExclusiveGateway_DeploysRoundtripAndCompletesOnlySelectedTask,`
  BrowserWorkflow_WaitingActivities_RoundtripAndInboxCompletionPreserveJoinSemantics,`
  BrowserWorkflow_CallActivity_PersistsSeparateChildAndContinuesParentOnlyAfterChildCompletion,`
  BrowserWorkflow_VersionDeployment_PinsRunningInstanceAndUsesV2ForNewInstance,`
  Analytics_TrainingCsvDownload_ContainsCompletedInstancesAndExcludesOtherTenantAndProcess
```

`Existing` bleibt für native PostgreSQL-/RabbitMQ-Installationen möglich.
Auf Linux steht `scripts/test-studio-e2e.sh` bereit; dessen `-TestMethod` nimmt
einen Methodenfilter je Aufruf. OIDC benötigt die separaten Keycloak-Scripts.
Es wurden keine GitHub-Workflows erweitert und keine lokalen Services gestartet.

## Nachweise und Theorie-Varianten

Der reale lokale Host schreibt künftig je Browser-Session in einen eindeutigen
Unterordner von `TestResults/studio-e2e/<RunId>/`:

- `scenario.json`: Run-ID, Session-ID, Methode, explizite Variante (neue Fälle),
  xUnit-Displayname einschließlich Theorieparameter, passende Katalog-IDs,
  Git-Revision/Dirty-Flag und SHA-256 von gebautem Katalog und Test-/Studio-/API-DLL.
- `playwright-trace.zip`, `final-page.png`, Browser-Konsole, fehlgeschlagene
  Requests sowie API-/Studio-Logs.
- `results.xml` und `results.html` des lokalen Runners im Laufverzeichnis.

Session-IDs verhindern, dass Theorie-Fälle oder wiederholte Aufrufe derselben
Methode ihre Traces überschreiben. Der Katalog wird beim Build ins Testoutput
kopiert; ein geänderter Quellkatalog etikettiert alte Binärdateien nicht um.
Metadaten sind ausschließlich Diagnose, **kein Erfolgssignal**. Ein Fehlerfall
erzeugt dieselben Metadaten. Fehlende DLL-Hashes sind `null`, nicht „bestätigt“.
Dirty-Checkout und abweichende DLL-/Katalog-Hashes müssen vor einer Abnahme
geprüft werden; eine Git-Revision allein beschreibt uncommittierten Code nicht.

Vorhandene zukünftige Laufartefakte rein lesend verknüpfen:

```powershell
./scripts/check-studio-use-case-coverage.ps1 `
  -ResultsDirectory 'tests/VertexBPMN.Studio.UiTests/TestResults/studio-e2e/<RunId>' `
  -AsJson
```

Nur ein eindeutig passender XML-Test-Displayname liefert dessen echtes Ergebnis;
sonst wird `unmatched` ausgegeben. Das Ergebnis gilt für den aufgezeichneten Lauf,
nicht automatisch den aktuellen Code oder alle Teilanforderungen. Alte Berichte
ohne `scenario.json` bekommen keinen erfundenen ID-/Trace-Nachweis. Die Keycloak-
und Stub-Hosts erhalten durch diese Änderung keine neuen Session-Metadaten.

## Historische Evidenz und verbleibende Arbeit

Der [Acceptance-Bericht](../reviews/2026-10-01_Studio-Acceptance-Korrekturen.md)
dokumentiert bei `ee1ff80` 99/99 lokale Fälle und separat 2/2 OIDC-Fälle.
Dieser Bericht bleibt historische Evidenz. Die neuen Fälle und die anschließend
gemergten Abhängigkeitsupdates sind dadurch **nicht** abgenommen.

Konkrete Restarbeit steht je ID in `Remaining`. Priorität:

1. UC05/UC07: echter HTTP-Connector, Response-Mapping und HTTP-Error-Boundary.
2. UC01/02/03/06/08/09: fehlende grafische Palette-/Properties-/Drilldown-Schritte;
   Import-/API-Setup nicht als diese Benutzerschritte ausgeben.
3. ADMIN-OAUTH/PLUGIN/SSO-CONFIG/TRAIN: echte Provider-/Plugin-/Trainingsabläufe.
4. M2.1–2.10: bislang fehlende Event-, Task-, Transaction-/Compensation-,
   Completion-Condition- und Collaboration-Varianten.
5. ERR-500/TIMEOUT/SUCCESS-DOUBLE: echte Serverfehler/Outages und simultane
   Mutationen statt nur wiederholter ungültiger Eingabe.
6. M2.11-WEBHOOK/SIMULATION und GUI-UNDO-REDO: automatischer Trigger samt Secret,
   Gateway-Traces Simulation vs. Server und Editor-Tastatur-/Undo-/Zoom-Abläufe.

**Gesamtstatus: vollständig inventarisiert, fachliche Acceptance-Abdeckung noch
nicht vollständig.** Ein grüner Build oder ein vollständig zugeordnetes Inventar
ist kein vollständiger UI-Abnahmebeleg.
