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
| C.9.0 | ⏳ | ⏳ | Pending |
| C.9.1 | ✅ | ✅ | Completed |
| C.9.2 | ✅ | ✅ | Completed |

**Completed: 17/21 · Pending (dokumentiert): 4/21**

## Feature: Auto-Start getypter Start-Events

Seit 2026-09-06 feuert die Auto-Instanziierung (`ProcessEngine.Execute`) getypte Start-Events
(message/timer/signal/…), wenn ein Prozess **kein** `none`-Start-Event besitzt. Dadurch wurden
**C.1.0, C.2.0, C.3.0 und C.6.0** (zuvor Pending) ausführbar — +4 auf Completed.

Regel (in `ProcessEngine.Execute`): Liefert ein Modell `none`-Start-Events, gelten nur diese
(Verhalten unverändert). Existiert keines, fallen alle getypten Top-Level-Start-Events als Startpunkte ein.

## Pending-Gründe (dokumentierte Lücken → Roadmap)

1. **C.8.0 / C.8.1 — interaktive DMN-Modelle:** Variable `'Vacation Approval'` wird erst durch
   Benutzer-/DMN-Eingabe gesetzt; die Engine erzwingt FEEL-Auswertung ohne diese Variable.
   → verdeutlicht Bedarf an **DMN-input-getriebener Auswertung**.
2. **C.1.1 — FEEL `bpmn:getDataObject(...)`:** Bedingung ist in der FEEL-Auswertung nicht implementiert.
3. **C.9.0 — FEEL-Quantor `some … in … satisfies`:** liefert `null` statt `boolean`; fehlende Quantor-Koerzion.

## Garantie der Suite

- **Regression:** Ein `Completed`-Modell, das plötzlich fehlschlägt → rot.
- **Unentdeckte Verbesserung:** Ein `Pending`-Modell, das plötzlich durchläuft → rot (zwingt zum Baseline-Update + Feature-Doku).
- **Vollständigkeit:** Fehlt eine Datei in der Baseline → verweigert den Lauf.

> Hinweis: Der frühere `xml.Replace('\'', '"')`-Hack wurde entfernt — er korrumpierte valide
> MIWG-Inhalte (Apostroph im Attributwert, z. B. `C.7.0.bpmn`) und erzeugte dadurch eine
> fehlerhafte `SecurityException` des XML-Validators. C.7.0 läuft seit dem Fix durch.
