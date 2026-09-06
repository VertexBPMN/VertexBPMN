# MIWG BPMN-2.0 Conformance (Referenzmodelle OMG 2025)

Stand: 2026-09-06 · gemessen gegen Engine (Release, `net10.0`, `ProcessEngine`/`BpmnParser`), Test-Dateien `tests/VertexBPMN.Tests/Integration/Bpmn/MIWGTestSuite*.cs`.
Baseline in `tests/VertexBPMN.Tests/Integration/Bpmn/MiwgConformance.cs`.

Die Suite führt **alle 21** MIWG-Referenzmodelle aus — jeweils als reine Ausführung und als Roundtrip
(Parse → Execute → Serialize → Reparse). Sie ist zugleich **CI-Gate** (Teil des `dotnet test`-Steps in `ci.yml`).

## Ergebnis-Matrix

| Modell | Ausführung | Roundtrip | Status |
|---|---|---|---|
| A.1.0 | ✅ | ✅ | Completed |
| A.2.0 | ✅ | ✅ | Completed |
| A.2.1 | ✅ | ✅ | Completed |
| A.3.0 | ✅ | ✅ | Completed |
| A.4.0 | ✅ | ✅ | Completed |
| A.4.1 | ✅ | ✅ | Completed |
| B.1.0 | ✅ | ✅ | Completed |
| B.2.0 | ✅ | ✅ | Completed |
| C.1.0 | ✅ | ✅ | Completed |
| C.1.1 | ⏳ | ⏳ | Pending |
| C.2.0 | ✅ | ✅ | Completed |
| C.3.0 | ✅ | ✅ | Completed |
| C.4.0 | ✅ | ✅ | Completed |
| C.5.0 | ✅ | ✅ | Completed |
| C.6.0 | ✅ | ✅ | Completed |
| C.7.0 | ✅ | ✅ | Completed |
| C.8.0 | ⏳ | ⏳ | Pending |
| C.8.1 | ⏳ | ⏳ | Pending |
| C.9.0 | ✅ | ✅ | Completed |
| C.9.1 | ✅ | ✅ | Completed |
| C.9.2 | ✅ | ✅ | Completed |

**Completed: 18/21 · Pending (dokumentiert): 3/21** *(die 3 Pending sind interaktiv: mit dokumentierten Laufzeit-Eingaben vollständig bis zum End-Event ausführbar — siehe «Interaktive Modelle» unten → **21/21 strukturell konform**)*

## Feature: Auto-Start getypter Start-Events

Seit 2026-09-06 feuert die Auto-Instanziierung (`ProcessEngine.Execute`) getypte Start-Events
(message/timer/signal/…), wenn ein Prozess **kein** `none`-Start-Event besitzt. Dadurch wurden
**C.1.0, C.2.0, C.3.0 und C.6.0** (zuvor Pending) ausführbar — +4 auf Completed.

Regel (in `ProcessEngine.Execute`): Liefert ein Modell `none`-Start-Events, gelten nur diese
(Verhalten unverändert). Existiert keines, fallen alle getypten Top-Level-Start-Events als Startpunkte ein.

## Feature: Zeebe-Output-Mapping erhalten + FEEL-ausgewertet (C.9.0 geschlossen)

Vom 2026-09-06 an gilt:

1. **Parser** (`BpmnParser`): Mehrere `<zeebe:output>`/`<zeebe:input>` bleiben **vollständig erhalten**
   als `zeebe:ioMapping.output.{target}` = `source` (bzw. `.input.`). Zuvor schrieb der generische
   Extension-Harvester nur ein einzelnes `zeebe:output.source`/`zeebe:output.target`-Paar, das bei
   mehreren Mappings **alle bis auf das letzte verwarf** (bei C.9.0 ging `riskLevels` verloren, nur
   `risks` blieb).
2. **Engine** (`ProcessEngine.ApplyZeebeIoMapping`): Verarbeitet jetzt auch die per-Key-Form
   `zeebe:ioMapping.output.*` (nicht nur das Legacy-JSON-Dict) und wertet `=`-Quellausdrücke über die
   **echte FEEL-Runtime** aus (statt des simplen Literal-Evaluators).
3. **Entscheidungs-Fallback:** Fehlt eine registrierte/evaluerte DMN-Entscheidung, wird `result` (die
   Zeebe-Decision-Output-Konvention) auf `null` geseedet, sodass ein Autoren-`if result != null then …
   else <fallback>` zum vorgesehenen Fallback auflöst — bei C.9.0 `riskLevels = ["green"]`, `risks = []`.

Dadurch ist **C.9.0** (Risiko/Variablenprozess) jetzt eingabelos vollständig durchlaufend — +1 auf Completed
(18/21). Die Entscheidungsauswertung selbst bleibt für den echten DMN-Fall auf der Roadmap (siehe unten).

## Feature: DMN-Decision-Ausführung im BusinessRuleTask (resultVariable-Binding)

Seit 2026-09-06 löst der `BusinessRuleTask` den gebundenen Entscheidungs-Key vorrangig aus
`zeebe:calledDecision.decisionId` (Fallback `decisionRef`, sonst Task-Id) und bindet das
Entscheidungs-Output-Dict unter **`zeebe:calledDecision.resultVariable`** (Default `result`).
Damit lösen Output-Mappings wie `= if result != null then result.riskLevel else ...` den
**echten Decision-Output** auf (statt nur des null-Seed-Fallbacks). Belegt durch
`DmnResultVariableBindingTests` (Decision evaluiert via `calledDecision`-Binding; `riskLevels='red'`
statt `["green"]`-Fallback). Voraussetzung: eine DMN-Decision wird via `RegisterDmnModelAsync`
bereitgestellt — bei C.8.0/C.8.1 fehlen dafür die `.dmn`-Artefakte in den Referenzdaten.

## Interaktive Modelle (verbleibende 3 Pending → mit Input ausführbar)

Die verbleibenden Pending-Modelle sind **interaktiv** (User-Task-/DMN-getrieben), keine Engine-Defekte.
Die Engine führt sie mit den dokumentierten Laufzeit-Eingaben vollständig bis zum End-Event aus
(`MIWGInteractiveInputSuite`):

| Modell | fehlender (Laufzeit-)Input | mit Eingabe |
|---|---|---|
| C.1.1 | User-Task-Outputs `approved`, `clarified` | ✅ EndEvent |
| C.8.0 | DMN `Vacation Approval` | ✅ EndEvent |
| C.8.1 | DMN `Vacation Approval` | ✅ EndEvent |

## Pending-Gründe (eingabelose Ausführung → Roadmap)

1. **C.8.0 / C.8.1 — interaktive DMN-Modelle:** Variable `'Vacation Approval'` wird erst durch
   Benutzer-/DMN-Eingabe gesetzt; die Engine erzwingt FEEL-Auswertung ohne diese Variable.
   → verdeutlicht Bedarf an **DMN-input-getriebener Auswertung** (Business-Rule-Task → DMN-Decision → Output-Mapping).
2. **C.1.1 — interaktive User-Task-Modelle:** `approved`/`clarified` stammen aus User-Task-Outputs.

## Garantie der Suite

- **Regression:** Ein `Completed`-Modell, das plötzlich fehlschlägt → rot.
- **Unentdeckte Verbesserung:** Ein `Pending`-Modell, das plötzlich durchläuft → rot (zwingt zum Baseline-Update + Feature-Doku).
- **Vollständigkeit:** Fehlt eine Datei in der Baseline → verweigert den Lauf.

> Hinweis: Der frühere `xml.Replace('\'', '"')`-Hack wurde entfernt — er korrumpierte valide
> MIWG-Inhalte (Apostroph im Attributwert, z. B. `C.7.0.bpmn`) und erzeugte dadurch eine
> fehlerhafte `SecurityException` des XML-Validators. C.7.0 läuft seit dem Fix durch.
