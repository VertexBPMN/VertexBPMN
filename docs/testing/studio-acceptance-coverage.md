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

## LC07 S1: Low-Code App-Designer (Foundation)

Der [Use-Case-Katalog](studio-use-cases.tsv) wurde um die Gruppe `LC07 S1`
ergänzt (IDs `LC07-S1-SERVICE`, `LC07-S1-API`, `LC07-S1-ALLOWLIST`,
`LC07-S1-DESIGNER`). Sie decken die neue Designer-Foundation ab:

- **API/SERVICE/API-Real**: `GET api/applications` Listen-Endpoint sowie der
  Studio-`HttpApplicationService`-Roundtrip (List/Create/Get/Save) gegen echte
  Controller- und Persistenzschicht (`CustomWebApplicationFactory`). Die aktuelle
  Nachverifikation ist grün; eine vollständige strikte TDD-Historie ist nicht belegt.
- **ALLOWLIST (Autorungs-Boundary)**: `AppDesignerDocument` erzwingt zu **jeder**
  Autorzeit die geschlossene Renderer-Allowlist aus
  `ApplicationComponentCatalog.Components` (Version `1.0.0`). Unbekannte
  Komponente/Version/Eigenschaft löst `InvalidOperationException` aus; unbekannte
  Eingaben können so nie in einer ContentJson landen. Palette = sortierte
  Allowlist. Vereinbarungsbedingt (Slice S1) verfügt der gültige Pfad noch **ohne**
  Datenquellen-Deklaration; `ApplicationDomainDiagnostics` würde eine freie
  `dataSource`-Referenz als `UNKNOWN_DATA_SOURCE` korrekt ablehnen (späterer Slice).
- **DESIGNER (browser-real)**: `ApplicationDesigner_CreateAddPageAddComponentEditPropertySaveAndReload_ARealDraft`
  deckt den Kernablauf Create App -> Add Page -> Add Component -> Edit Property ->
  Save -> Reload über echte API und isoliertes PostgreSQL ab. Tatsächlich ausgeführt:
  beide Designer-Browserfälle zusammen 2 bestanden, 0 Fehler/Failures/Skips.
  SQL-Assertions prüfen Draft und Revision 2 unabhängig von der Erfolgsmeldung.
  Reload eröffnet eine neue Browsernavigation, wählt den Tenant erneut und öffnet
  den Designer im selben Circuit. Tenantwechsel verwirft den geladenen Entwurf.
  Echter Verhaltens-RED für Tenantwechsel und anschließendes GREEN sind vorhanden.

## LC07 S2: Datenquellen + Datenbindung + Schema-validiertes Feld-Mapping

Der [Use-Case-Katalog](studio-use-cases.tsv) wurde um die Gruppe `LC07 S2`
ergänzt (IDs `LC07-S2-DOC`, `LC07-S2-SERVICE`, `LC07-S2-DESIGNER`). Sie bauen
additiv auf S1 auf und decken die Datenquellen-Schicht des Designers ab:

- **Deklaration**: `AppDesignerDocument` führt `DataSources` (Key + bindingVersion
  + authored Schema `fields`), `AddDataSource`/`RemoveDataSource`. Die serialisierte
  `dataSources`-Deklaration trägt `key`/`bindingVersion` in der exakten
  Lc06ReferenceApp-Form; `fields` ist eine additive Autorzeit-Schemainformation, die
  von allen Released-Readern (key/bindingVersion) toleriert und im Draft roundgetrippt
  wird. Doppelte Keys werden abgelehnt.
- **Bindung**: `BindComponent` erzwingt, dass eine gebundene Komponente auf eine
  DEKLARIERTE Quelle verweist; Binding an eine undeklarierte Quelle wird zur
  Autorzeit geworfen. Jede Referenz, die dennoch aus externem Content stammt
  (oder nach `RemoveDataSource` übrig bleibt), wird von `Validate()` als
  `UNKNOWN_DATA_SOURCE` gemeldet — identisch zur Released-Semantik von
  `ApplicationDomainDiagnostics` (deren `Validate(json)` direkt wiederverwendet wird).
- **Schema-validiertes Feld-Mapping**: `MapField` (Datenquellen-Feld → Tabellen-
  Spalte/Komponenten-Feld) akzeptiert nur Felder, die auf dem gebundenen Schema
  existieren; unbekannte Felder werden zur Autorzeit geworfen und nie still
  akzeptiert. Die Mapping-Struktur folgt der FieldMapper-Konvention
  (`StudioFieldMappingItem`: sourcePath → targetKey).
- **API/SERVICE-Real**: `SaveDraft_WithDeclaredDataSourceBoundTableAndMappedField_RoundtripsThroughRealApi`
  persistiert einen Draft mit deklarierter Quelle + gebundener Tabelle + gemapptem
  Feld durch echte Controller-/Persistenzschicht (`CustomWebApplicationFactory`) und
  lädt ihn wieder (declare -> bind -> map -> save -> reload). Nachverifikation:
  `AppDesignerDataBindingTests` 9 bestanden; die vier relevanten Logic-Klassen
  zusammen 22 bestanden, 0 Fehler/Failures/Skips. Strikte TDD für S2 ist **nicht**
  bewiesen: Compiler-/Analyzerfehler sind kein Verhaltens-RED.
- **DESIGNER (browser-real)**: `ApplicationDesigner_DeclareDataSourceBindTableMapFieldSaveAndReload_ARealDraft`
  deckt den Ablauf Declare Data Source -> Bind Table -> Map Field -> Save -> Reload
  über echte API und isoliertes PostgreSQL mit unabhängigen SQL-Assertions ab.
  Tatsächlich ausgeführt und bestanden; nach Reload werden Quelle, Bindung und
  Mapping im Property-Editor geprüft. Die Auswahl enthält genau die drei
  selbst deklarierten Schemafelder.

**Evidenz (lokal, sanitisiert):** `/home/azureuser/.hermes/cache/scratch/lc07-verification/`.
Browser/SQL-GREEN: `lc07535e29f44c/results.xml`, `runner.log`, scenario-spezifische
Playwright-Traces, Screenshots und `database-cleanup.log`. Tenantwechsel-RED:
`lc07b6a7532280/results.xml`; GREEN: `lc07fdf3b8ee20/results.xml`.
Logic-Nachverifikation: `logic-final.xml` (22/0). UiTests-Baseline: zwei bereits
in HEAD vorhandene Analyzerfehler in `StudioUiTestHost.cs` (IDE0011/MA0051),
minimal repariert; finaler Release-Build ohne Warnungen/Fehler.

**Weiter offen vor vollständiger LC07-Abnahme:** erzwungene verspätete Load-/Save-
Antworten sind noch nicht dynamisch getestet (Generation-Guards vorhanden), ebenso
App-Listen-Tenantwechsel/verspätete Listen-/Create-Antworten. `fields` ist ein
selbst deklariertes Schema, kein Nachweis eines aus LC03–LC06 aufgelösten echten
Datenquellenschemas. Mapping-Persistenz beweist keine Portal-Mappingausführung.
Parse/Serialize erfasst nicht alle vorhandenen App-Metadaten (u.a. Seitentitel,
Lokalisierungen/Referenzen); verlustfreies Editieren bestehender Voll-Drafts bleibt
zu prüfen. S3 wurde nicht begonnen; S1/S2 sind nicht pauschal vollständig abgenommen.

**Scope**: S2 bildet ausschließlich Datenquellen-Deklaration, -Bindung und
Schema-validiertes Feld-Mapping samt Roundtrip ab. Action-Auswahl,
Start/Claim/Complete-Verdrahtung, Preview und Undo/Redo gehören zu späteren Slices
und wurden nicht angelegt.

## LC07 S3: Action (Button-Ausführung)

Der [Use-Case-Katalog](studio-use-cases.tsv) wurde um die Gruppe `LC07 S3`
ergänzt (IDs `LC07-S3-DOC`, `LC07-S3-SERVICE`, `LC07-S3-DESIGNER`). Sie
bauen auf S1/S2 auf und machen die Button-Ausführung explizit.

**Umgesetzt (LC07-S3-DOC, Unit):** Eine Komponente trägt höchstens eine
zugelassene Aktion aus dem geschlossenen Katalog (`data-source-read`,
`start-process`, `claim-task`, `complete-task`, alle `v1.0.0`). Das erzeugte
Aktions-JSON entspricht der LC06-Referenzgestalt (z.B. `start-process` mit
`processDefinitionId` = stabilem Prozess-Key plus `references[].{type:process,key}`).
Referenz-Targets werden über geschlossene Kataloge gewählt, nicht handschriftlich:
`data-source-read` → deklarierte Datenquelle (undeclared →
`UNKNOWN_DATA_SOURCE`), `start-process` → bekannter Prozess-Key aus dem
Prozess-Katalog (`unknown → UNKNOWN_PROCESS`), `claim/complete-task` →
Aufgaben-Key mit explizitem Input. Unbekannte/unsanktionierte Aktions-Typen
werden geworfen und nie in ContentJson emittiert. Das Server-Gate
`ApplicationDomainDiagnostics` prüft dieselben Regeln.

**HTTP-Roundtrip (LC07-S3-SERVICE):** Ein Draft mit Button + konfigurierter
Aktion (Referenz-Target + explizites Input) wird über die echte API persistiert,
geladen und inhaltsgleich zurückgegeben; der gespeicherte Content besteht das
freigegebene Validierungs-Gate (`[]`).

**Browser (LC07-S3-DESIGNER):** `ApplicationDesigner_ConfigureActionOnButtonSaveReloadAndFlagDanglingReference_ARealDraft`
— Add Button → Add Action (Datenquelle ausgeschlossen) → Referenz-Ziel
(deklarierte Quelle) → explizites Input → Save → Reload, gegen echte API +
isoliertes PostgreSQL mit unabhängigen SQL-Assertions. Nach Reload werden
Aktion, Referenz-Ziel und Input im Property-Editor geprüft. Negativ:
Entfernen der referenzierten Datenquelle lässt die Aktion baumeln; der Designer
flaggt `UNKNOWN_DATA_SOURCE` statt sie stillschweigend zu akzeptieren.

**Evidenz (lokal, sanitisiert):** siehe `lc07s3-e2e.log`/Ergebnis-XML unter
`/home/azureuser/.hermes/cache/scratch/`.

**Weiter offen vor vollständiger LC07-Abnahme:** Cross-Editor-Verdrahtung
Prozess/Form/App/Daten-Mapper (Referenzen statt Hand-Kopieren technischer Keys
in den Beny/BPMN-Editoren), Preview und Undo/Redo gehören zu späteren Slices
und wurden nicht angelegt. Start/Claim/Complete zur Laufzeit starten keinen
Prozess; S3 beweist Authoring-/Persistenz-/Gate-Verhalten, nicht die
ausgeführte Prozessausführung.

**Scope:** S3 bildet ausschließlich den Aktions-Editor (geschlossener Katalog,
referenzbasiertes Ziel, explizites Input/erwartetes Ergebnis, Validierung +
Roundtrip + Tests) ab.

## LC07 S4: Undo/Redo + Preview + Workspace-Härtung

Der [Use-Case-Katalog](studio-use-cases.tsv) wurde um die Gruppe `LC07 S4`
ergänzt (IDs `LC07-S4-UNDO-DOC`, `LC07-S4-PREVIEW-DOC`,
`LC07-S4-UNDO-DESIGNER`, `LC07-S4-PREVIEW-DESIGNER`). S4 baut additiv auf
S1..S3 auf, ohne ein zweites Darstellungs-/Entwurfsverfahren einzuführen.

**Undo/Redo (LC07-S4-UNDO-DOC, Unit):** Der DesignDocument-Draft trägt einen
gebundenen Command-Stack (`MaxUndoDepth = 200`) aus unveränderlichen
`DocumentState`-Tiefenschnappschüssen. Jeder Mutator ruft `RecordHistory()`
unmittelbar **vor** seiner Änderung auf. Add/Remove/Reorder Page + Component,
Property-, Datasource-, Binding-, Action- und Mapping-Änderung sind alle
udno/redo-fähig. `Undo()`/`Redo()` stellen den Baum mit geklonten Objekten
wieder her; Konsumenten müssen Seiten/Komponenten über stabile String-IDs über
`document.Pages` neu auflösen (der Surface tut das bereits). `CanUndo`/`CanRedo`
sind während einer Wiederherstellung deaktiviert. Ein frischer Edit verwirft den
Redo-Zweig (kein Stale-Snapshot-Re-Save). `UndoRedoTests` = 14 Unit-Fälle;
save-Serialisierung folgt stets dem **live**-Modellstand (`ToJson()`), wie im
Test `SaveSerializesCurrentState_AfterUndoAndRedo_NoStaleSnapshot` belegt.

**Preview (LC07-S4-PREVIEW-DOC + DESIGNER):** Der Preview modelliert den
aktuellen DRAFT (nur Allowlist-Komponenten, gespiegelt von
`ReleasedRenderer`/`ApplicationComponentCatalog.Components` — kein ad-hoc
Renderer) gegen ein **explizit gewähltes Test-Ziel** (z.B. eine Datenquelle
`invoiceList` oder `standard`) mit **redigierten Testdaten**
(`[redigiert:<feld>...]`). Preview ist **rein lesend**: `AppPreview.ReadOnly =
true`, `ExecutableActions = []` (leer), side-effecting Aktionen
(`start-process`/`claim-task`/`complete-task`/extern) werden nur als
`ActionIsSideEffecting` markiert und als non-executing gerendert — sie werden
**niemals ausgeführt**. Unbekannte Komponenten → `Unsupported`-Platzhalter.
`PreviewTests` = 6 Unit-Fälle.

**Browser (LC07-S4-UNDO-DESIGNER, LC07-S4-PREVIEW-DESIGNER):**
`ApplicationDesigner_UndoRedoComponentAddition_KeepsDraftConsistent` —
Add Component → Toolbar-Undo/Redo + Ctrl+Z/Ctrl+Shift+Z → dirty zwischen
"Gespeichert"/"Unstete Änderungen"; über die echte API/SQL wird bestätigt, dass
die Button-Änderung **nie persistiert** wurde (Revision bleibt 2).
`ApplicationDesigner_PreviewRendersDraft_WithoutFiringProductiveStart` — ein
Button mit `start-process`-Aktion auf einen im Tenant gedeployten Prozess-Key +
deklarierte Datasource wird per Preview gerendert (redigierte Testdaten +
non-executing-Marker + Banner); der **unabhängige** DB/API-Beweis (nicht eine
UI-Erfolgsmeldung) zeigt: keine neue Prozess-Instanz, kein Task, kein
Draft-Write während des Vorschauens.

**Workspace-Härtung:** Save serialisiert immer den **live**-Draftstand
(delayed-response-sicher — ein langsamer In-Flight-Save kann weder neuere Edits
noch veraltete Redo-Zustände überschreiben), Undo/Redo laufen rein in-memory
ohne Persistenzseiteneffekt, Undo/Redo sind während `_saving` deaktiviert, und
der Tenant-Wechsel invalidiert den fremden Draft weiterhin (bestehende
S1-Fixierung bleibt erhalten). Kein S5 (Publish/Build der Referenz-App).
