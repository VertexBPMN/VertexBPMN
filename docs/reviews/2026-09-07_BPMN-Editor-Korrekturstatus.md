# BPMN-Editor: Korrekturstand

Grundlage: „Plan zur Korrektur der gefundenen BPMN-Editor-Fehler.md“ vom Nutzer.
Stand: 2026-09-07. Die unten erfassten Editor-Korrekturpunkte sind implementiert und lokal geprüft. Dies ist keine vollständige Produkt- oder BPMN-Konformitätsabnahme.

## Implementiert

- [x] Einzelne Katalogaktivitäten benötigen einen ausgewählten Sequenzfluss; ohne Auswahl erfolgt eine erklärende Fehlermeldung vor einer Modelländerung.
- [x] Einfügen verbindet Quelle → neue Aktivität → bisheriges Ziel. Bedingungen, Name, Dokumentation, Erweiterungen und Default-Zuordnung des bisherigen Flows werden übernommen.
- [x] Globale Connector-Templates verwenden ebenfalls diesen Einfügepfad. Quick-Insert wird nur an Sequenzflüssen angeboten.
- [x] Einzelnes IF-Einfügen erzeugt Split, Otherwise-Zweig, Then-Aktivität und Merge. Der Bedingungszweig wird ausgewählt und muss konfiguriert werden.
- [x] Start-/End-Events und Event-Subprozesse werden beim einzelnen Katalogeinfügen mit einem Hinweis auf Pattern bzw. BPMN-Palette abgewiesen; sie werden nicht in einen Sequenzfluss eingespleißt.
- [x] Strukturprüfung im Studio für Modell und XML: Flow-Endpunkte innerhalb eines Scopes, unerreichbare Knoten bei expliziten Start-Events, fehlende ausgehende XOR/OR-Flows und unkonfigurierte nicht-default XOR/OR-Zweige.
- [x] Serverseitige Prüfung des geparsten Modells vor dem Repository-Schreibzugriff. Deploymentfehler enthalten strukturierte Diagnosen; der Repository-Controller gibt dafür HTTP 400 zurück.
- [x] Studio blockiert Deploy und Engine-Test bei Validierungsfehlern. Fehlende Editor-/Validator-Bundles werden nicht als erfolgreiche Validierung behandelt.
- [x] Boundary-Aktivierungen, Link-Sprünge, unabhängige Event-Subprozesse und Kompensationsaktivitäten werden bei der Erreichbarkeit berücksichtigt. Scopes ohne expliziten Start dürfen weiterhin implizite Starts verwenden.
- [x] Parserkorrektur: Das Standardattribut `isForCompensation` wird für Tasks und Subprozesse ins Laufzeitmodell übernommen.
- [x] Vorhandener lokaler GUI-Test für Decision-/Form-Katalogeinfügen ergänzt: Auswahl eines Flows und tatsächliche Ein-/Ausgangsverbindungen werden geprüft.

Die Deploymentregeln sind Produktprüfungen für ausführbare Modelle, kein Nachweis vollständiger BPMN-2.0-Konformität. Insbesondere wird nicht pauschal gefordert, dass jede Aktivität einen expliziten End-Event erreichen muss. Die bisherigen Advisory-Regeln des Roundtrip-Parsers bleiben unverändert.

## Verifikation: tatsächlich ausgeführter Stand

- [x] JavaScript: **9/9 Tests** mit echtem `bpmn-moddle` bestanden, einschließlich gemeinsamer Client-/Server-Fixtures.
- [x] Backend/CLI: **16/16 gezielte Tests** aus der regulären Testassembly bestanden (`BpmnDeploymentValidatorTests`, `Phase3AdvancedValidationReachabilityTests`, `CliApplicationTests`). Darunter defektes XML, Kompensation und semantische Validierung.
- [x] Chromium: **1/1 umfangreicher Modelliertest** bestanden; prüft Einfügen, Default-Erhalt, Fehlerfokus, XML-Validierung, sechs Patterns und atomisches Undo/Redo. Screenshots unter `tests/VertexBPMN.Studio.UiTests/TestResults/editor-corrections/` visuell geprüft.
- [x] Echte lokale Studio/API/Engine mit WSLC: **6/6 E2E-Fälle**, keine übersprungenen Tests. Lauf `365d532db0fd426383d4d8c65d4c475e`; Bericht: [results.html](../../tests/VertexBPMN.Studio.UiTests/TestResults/studio-e2e/365d532db0fd426383d4d8c65d4c475e/results.html).
- [x] Browser-Bundles neu gebaut; API, Studio, CLI und reguläre Backend-Testassembly erfolgreich gebaut. Bestehende Analyzerwarnungen bleiben; der UI-Restore meldete außerdem NU1900 wegen nicht erreichbarer NuGet-Sicherheitsdaten. Keine Aussage über eine vollständig aktuelle Schwachstellenprüfung.
- [x] Änderungsprüfung ohne Whitespacefehler in den bearbeiteten Korrekturdateien. Fremde Änderungen in `ProcessDefinitions.razor` und lokalen Datenbankdateien bleiben unangetastet.

Die früheren Freigabe-/Dateizugriffsblockaden sind für diese Wiederholungen überwunden. Es wurde keine vollständige Projekttestsuite ausgeführt.

## Abgeschlossene zuvor offene Planpunkte

- [x] **UserTask-Lebenszyklus:** Katalogeinfügen und Deployment im echten Studio; Aufgabe in der Inbox übernehmen und abschließen; persistierten Prozessabschluss und leere Aufgabenliste prüfen.
- [x] **IF-Ausführung:** Im Studio erzeugtes/exportiertes Diagramm mit konfigurierter Bedingung deployen; mit `amount=5000` und `amount=10` starten. Historie belegt den jeweiligen Flow am konkreten Split-Gateway und das Erreichen des Merge. Split und Merge werden nicht über die zufällige Reihenfolge der Historieneinträge verwechselt.
- [x] **API-Abweisung:** Isolierte UserTasks/Gateways liefern HTTP 400 mit Diagnosen und werden nicht gespeichert. Syntaktisch defektes XML liefert ebenfalls HTTP 400; die vom Ressourcenprüfer ausgelöste Domain-`SecurityException` wird korrekt behandelt.
- [x] **Pattern-Katalog:** `http-retry`, `webhook-if-http`, `cron-batch-db`, `user-approval`, `decision-routing`, `case-start` im echten Modellierer geprüft. Beide IF-Patterns besitzen Bedingungs- und Default-Zweig; eine noch leere Bedingung bleibt ein bewusst zu konfigurierender Validierungsfehler.
- [x] **Client-/Server-Parität:** Gemeinsames XML-Korpus für implizite Starts, verschachtelte Scopes, mehrere Prozesse und einen isolierten Knoten im Subprozess. Prüfung durch Moddle, Browser-XML-Validator, Backend-Modellvalidator und SemanticValidationService.
- [x] **Layout und Undo/Redo:** Mehrteilige Einfügungen bilden eine einzige rückgängig machbare Aktion. IF-Insertion schafft Platz; Pattern-Default-Flows werden unterhalb der Aktivitäten geführt statt durch sie hindurch. Screenshots und Undo/Redo-Assertions liegen vor.
- [x] **Semantische Validierung:** `SemanticValidationService` verwendet jetzt den Parser und denselben Deploymentvalidator. `ValidateBpmnAsync` und scoped Registrierung eingeführt; API- und CLI-Aufrufer angepasst. DMN-Validierung unverändert.
- [x] **Fehlernavigation:** Validierung selektiert und scrollt zum fehlerhaften darstellbaren Element; im Chromiumtest am unkonfigurierten IF-Zweig nachgewiesen. Deploy/Engine-Test bleiben bei Fehlern gesperrt.

### Grenzen der Abnahme

Die sechs Patterns sind hinsichtlich Editorstruktur, Validierung, Layout und Undo/Redo geprüft. Die tatsächliche Ausführung sämtlicher externer HTTP-/Datenbank-/Case-Integrationen ist damit nicht belegt und benötigt deren reale Konfiguration. Nicht konfigurierte Bedingungen werden nicht durch Dummy-Werte ersetzt. Die Screenshots stammen aus dem isolierten echten Modellierer; vollständige Studio-Abläufe sind durch die gesonderten E2E-Fälle abgedeckt. Der gesamte frühere GUI-Testplan wurde hier nicht erneut ausgeführt.

## Lokale Wiederholung

Keine CI-Änderungen wurden vorgenommen. Der Chromiumtest ist explizit über `VERTEXBPMN_EDITOR_TESTS=1` freizuschalten.

```powershell
# Aus src/VertexBPMN.Studio:
npm run test:flow-validation
npm run build:bpmnio

# Aus dem Repository-Root; verwendet getrennte lokale Build-Verzeichnisse:
dotnet build tests/VertexBPMN.EditorValidation.Tests/VertexBPMN.EditorValidation.Tests.csproj -p:BaseIntermediateOutputPath=obj-editor-fix/ -p:BaseOutputPath=bin-editor-fix/
dotnet tests/VertexBPMN.EditorValidation.Tests/bin-editor-fix/Debug/net10.0/VertexBPMN.EditorValidation.Tests.dll

# Nur nach erfolgreichem explizitem Asset-Build:
dotnet build tests/VertexBPMN.Studio.UiTests/VertexBPMN.Studio.UiTests.csproj -p:BaseIntermediateOutputPath=obj-editor-fix/ -p:BaseOutputPath=bin-editor-fix/ -p:SkipBpmnIoAssetBuild=true
$env:VERTEXBPMN_EDITOR_TESTS='1'
dotnet tests/VertexBPMN.Studio.UiTests/bin-editor-fix/Debug/net10.0/VertexBPMN.Studio.UiTests.dll -class '*BpmnEditorInsertionTests'
```

Zusätzlich aus dem Repository-Root:

```powershell
dotnet build tests/VertexBPMN.Tests/VertexBPMN.Tests.csproj -c Release --no-restore -p:SkipBpmnIoAssetBuild=true
dotnet tests/VertexBPMN.Tests/bin/Release/net10.0/VertexBPMN.Tests.dll -class '*BpmnDeploymentValidatorTests' -class '*Phase3AdvancedValidationReachabilityTests' -class '*CliApplicationTests'
./scripts/test-studio-e2e.ps1 -Infrastructure Wslc -TestMethod 'EditorCorrections_*'
```

Der E2E-Lauf verwendet isolierte Testdatenbanken und beendet seine API-/Studio-Prozesse anschließend. Die vorhandene WSLC-Infrastruktur bleibt laufen. Testberichte/Screenshots sind lokale, nicht eingecheckte Artefakte.
