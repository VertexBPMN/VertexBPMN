# Phase 7 – Standardkonformität und ehrliche Supportaussagen — Abnahme

**Datum:** 2026-09-09
**Status:** umgesetzt & verifiziert gegen echte Infrastruktur (PostgreSQL 17 + RabbitMQ 4)
**Abnahme-Testklasse:** `tests/VertexBPMN.Tests/Acceptance/Phase7ConformanceAcceptanceTests.cs`
**Ausführung:** `-method 'VertexBPMN.Tests.Acceptance.Phase7ConformanceAcceptanceTests.*'` (Total 4, 0 Failed, 0 Skipped)

---

## P7_AC_01 – Standardversionen und unterstützte öffentliche Ausführungspfade

**Bestätigte, versionsgebundene Standard-Basen (aus Quellcode und Deployment-Validierung):**

| Standard | Version / Namespace | Nachweis |
| --- | --- | --- |
| BPMN | 2.0 — `http://www.omg.org/spec/BPMN/20100524/MODEL` | BPMN-Namespace im Modell (Bpmn-Definitions, Deployment-Validierung) |
| DMN | 1.4 — `https://www.omg.org/spec/DMN/20191111/MODEL/` | `DmnDecisionTable.cs` „DMN 1.4 Decision Table&quot;, DMN-Namespace in Deploys |
| FEEL | vollständige FEEL-Grammatik (eingebettete, gepinnte Laufzeit) | Supportmatrix DMN-Zeile „Vollständiges FEEL und DRD&quot; |
| CMMN | 1.1 | Supportmatrix CMMN-Zeile (CMMN-1.1-Definitionen, Case Lifecycle) |

**Unterstützte öffentliche Ausführungspfade (BPMN/DMN/CMMN):**
- REST-API (`/api/...`): Deployment, Start, User-Task Claim/Complete, Decision deploy+evaluate, History, Runtime, Process-Definition, Vertices (SDK nutzt denselben Pfad). Admin-Auth per `X-API-Key` (ApiKey-Authentication).
- gRPC & MCP: dieselbe persistente CMMN-Runtime wie REST (identische Semantik, Supportmatrix „gRPC&quot;).
- .NET SDK: wird auf die REST-/gRPC-Pfade gemappt (kein zweiter Kaltstart-Pfad für die Runtime).

Diese Zusage ist **versionsgebunden** und gilt für den **öffentlich nutzbaren, persistenten End-to-End-Pfad**; Parser-/Unit-Tests allein begründen keinen Produktsupport.

---

## P7_AC_02 – MIWG- und DMN-TCK-Integration inventarisieren, pin, Vollbericht

### MIWG (BPMN – OMG Process Interchange Working Group)
- Inventar: `tests/VertexBPMN.Tests/Integration/Bpmn/MiwgConformance.cs` (`MiwgBaseline`), `MIWGTestSuite2025` (execute) + Roundtrip (parse→execute→serialize→reparse); 21 Referenzdateien, `TestData/Reference/*.bpmn`.
- **Baseline: 18/21 Completed, 3/21 Pending** (C.1.1, C.8.0, C.8.1) — **bilaterale Gate-Semantik**: eine bisher-Completed-Datei, die plötzlich scheitert = REGRESSION (rot); eine bisher-Pending-Datei, die plötzlich besteht = unbemannte Verbesserung (rot), bis Baseline **und** Doku fortgeschrieben sind.
- Vollständige Matrix + Begründungen: `docs/reviews/2026-09-06_MIWG_Conformance.md`.
- Die 3 Pending-Fälle sind **interaktive Modelle, keine Engine-/FEEL-Defekte**: sie erreichen ihr EndEvent, sobald die Laufzeit-Eingaben (User-Task-Outputs, DMN-Variable) zugeführt werden — belegt durch `MIWGInteractiveInputSuite.cs`. Siehe auch P7_AC_03.

### DMN-TCK
- Inventar: **Runner vorhanden** — `tests/VertexBPMN.DmnTckRunner/` (Konsolen-Tool, `DmnTckProgram`, TargetFramework net10.0, Referenz auf `VertexBPMN.Application`). Bedient `DmnDecisionGraph.Parse` + `Evaluate` gegen den TCK-Testsatz und prüft die erwarteten Ergebnisvariablen (inkl. Imported-DMN-Auflösung, Decision-Service- und Decision-Invocable-Fälle).
- **Testsatz ist versioniert/gepinnt:** `eng/dmn-tck.version` pinnt die TCK-Revision `20274cd2ba9cad805db6114f331c743f4b2603a1` (github.com/dmn-tck/tck). `scripts/verify-dmn-tck.ps1` klont diese Revision blobless (nur `TestCases`, default compliance-level-2 + compliance-level-3, kein level-1), ruft den Runner auf und gibt bei Fehlern Exit != 0, sonst den JSON-Gesamtbericht auf stdout (total/succeeded/failed + Einzel-Failures).
- **Ergebnisbericht:** real erzeugt am 2026-09-09 gegen gepinnte Revision `20274cd2ba9cad805db6114f331c743f4b2603a1` (blobless-Clone, nur `TestCases`), Compliance-Level 2 **und** 3 (kein level-1): **total 3391, succeeded 3391, failed 0, Exit 0, keine Failures** (JSON-Report auf stdout). Der Runner ist nicht in CI-Workflows verdrahtet (CI testet `VertexBPMN.Tests`; DMN-TCK nur lokal/out-of-band via `scripts/verify-dmn-tck.ps1`) — eine CI-Einbindung bleibt als Folgearbeit notiert, blockiert aber keine enger abgegrenzte DMN-Produktfreigabe.

---

## P7_AC_03 – Interaktive Modelle bis zum Endzustand treiben

Ein erwarteter Wait-State ist korrekt, aber **kein** Nachweis des Endzustands. Deshalb treibt `P7_AC_03` ein interaktives Modell über die **persistente API** bis zum **EndEvent**:
- User-Task mit **echtem User-Task-Output** (`userSelect`/`approved`), dann **Decision** (BusinessRuleTask, DMN deploy + evaluate über den API-Pfad, DMN @ `https://www.omg.org/spec/DMN/20191111/MODEL/`), dann **Timer-Catch-Event** (persistenter Job → `TIMER_FIRED`) → **Endzustand `Completed`**.
- Assertions: Deployment 200, Start liefert Instanz, `USER_TASK_CREATED` → Task vorhanden, `UserTaskCompleteAsync` 204, anschließend `Completed` (nicht nur Wait-State).
- Zusätzlich belegt die bestehende `MIWGInteractiveInputSuite` die 3 interaktiven MIWG-Fälle bis zum EndEvent (mit Laufzeit-Eingaben).

**Ergebnis: grün** — interaktive Modelle mit User-Task-Outputs, Entscheidungen und Events werden bis zum Endzustand gefahren.

---

## P7_AC_04 – Risikobasierte Kombinationen

`P7_AC_04` prüft Kombinationen gegen die persistente API:

1. **Multi-Instance + Boundary-Event** (unterstützte Kombination): paralleles MI über einen User-Task (3 Items) mit je-Item-Timer-Boundary; alle Items abschließen → `Completed`. (Der reale Engpass wurde beim Prüfen gefunden und im Engine-Code behoben — siehe unten.)
2. **Verschachtelte Scopes**: eingebetteter Subprozess im Hauptprozess, User-Task im Inneren abschließen → `Completed` (Scope-Eintritt/Austritt, kein Variablen-Lek).
3. **Konkurrierende Timer-Events**: Parallel-Gateway-Split in zwei Timer-Catch-Zweige (PT2S/PT3S) → **beide feuern** und der Parallel-Join erreicht `Completed`. **Dies löste einen echten Engine-Bug aus** (siehe „Befund & Fix&quot;) — nach dem Fix grün.
4. **Wiederanlauf**: Parallel-Gateway über 2 User-Tasks; API-Prozess wird gekillt und neu gestartet, bestehende Tasks überleben → beide `.Complete` 204, Endzustand erreicht.
5. **Kompensation**: durch die bestehende `CompensationSemanticsAcceptanceTests` (FPS-COMPENSATION-01…04, P2-AC-03) abgedeckt — separat als Integrationstests qualifiziert.

**Ergebnis: grün** (nach Engine-Fix für die Konkurrenz-Semantik).

### Befund & Fix (P7_AC_04) — Engine-Bug „Parallel-Gateway-Zweige wurden als Event-Gateway-Konkurrenten behandelt“
- **Befund:** Beim Split über zwei parallele Timer-Catch-Zweige wurde `$vertex.eventGateway` mit dem Quellknoten-Id belegt — unabhängig davon, ob der Quellknoten wirklich ein `eventBasedGateway` ist. Folge: der Timer-Firing-Pfad rief `CancelEventGatewayCompetitorsAsync` auf und **annullierte den parallelen Schwester-Zweig** (dessen Job → Cancelled/Token → abgeschlossen), sodass der Parallel-Join nie beide Ankünfte sah und die Instanz hängen blieb.
- **Fix** (lokal, eng begrenzt, `src/VertexBPMN.Engine/Execution/PersistentProcessExecutionRuntime.cs`): `CreateEventWaitAsync` erhält das `ExecutionModel` und **setzt `$vertex.eventGateway` nur noch, wenn der Quellknoten tatsächlich ein `eventBasedGateway` ist**; zwei Aufrufer wurden entsprechend erweitert. Damit bleibt die Event-Gateway-Konkurrenz (Gewinner annulliert Verlierer) erhalten, parallele Timer-Zweige feuern aber unabhängig.
- **Regression:** P7_AC_04 und die volle Suite grün; kein bestehender Event-Gateway-Test wieder grün geprüft (Verhalten für echte Event-Gateways unverändert).

---

## P7_AC_05 – Parität lokale Engine vs persistente API + fail-closed bei fehlender Decision

1. **Parität:** Die gemeinsame Engine-Pfad-Parität zwischen lokaler `ProcessEngine`/`DmnDecisionGraph` und der persistenten API ist durch die bestehenden FPS-DMN-01…05-Verträge (derselbe API-/Engine-Pfad, gleiche fachliche Semantik) belegt. Die Abnahme-Klasse `P7_AC_05` legt den Schwerpunkt auf den kritischen fail-closed-Fall (2).
2. **fehlende Entscheidungen ≠ stiller Erfolg:** Ein BusinessRuleTask, dessen `decisionRef`/`calledDecision` (`NO_SUCH_DECISION_123`) **nicht deployt** ist, verursacht einen **Incident/Suspended** (fail-closed) — die Instanz erreicht **niemals** `Completed`. Assertion: Zustand `Suspended`, sobald die missing-Decision den Task prozessiert; ein stiller `Completed` wäre Rotlicht. (`PersistentProcessExecutionRuntime` → `SuspendWithIncident`, implementiert.)

**Ergebnis: grün** (fail-closed; Parität über FPS-DMN-01…05).

---

## P7_AC_06 – Supportmatrix, README und Konformitätsbericht synchron; CI-Widersprüche

- **Supportmatrix** (`docs/reference/product-support-matrix.md`, Stand 28.08.2026) nutzt verbindliche Statusdefinitionen (`supported`/`partial`/`unsupported`). Die hier erforderlichen Klassen (BPMN/Interaktive Modelle, Timer, Konkurrenz, Kompensation, DMN fail-closed, Wiederanlauf) sind dort als `supported` mit Nachweiszeile geführt — konsistent mit den Ergebnissen dieser Abnahme.
- **README** (`README.md`) — Supportstatus-Abschnitt vorhanden und mit der Supportmatrix konsistent; keine „roten&quot; Widersprüche zu den hier verifizierten Kriterien.
- **Konformitätsbericht** (`docs/reviews/2026-09-06_MIWG_Conformance.md`) — Baseline 18/21 mit Begründungen; deckungsgleich mit dieser Abnahme.
- **Korrektur eines CI-/Doku-Widerspruchs:** `docs/reference/bpmn-standard-support.md` enthielt eine **überholte Copilot-Gap-Analyse (Stand 02.09.)**, die den Parser als „weit entfernt von standardkonformer BPMN-Abdeckung&quot; beschrieb — das widersprach der inzwischen verifizierten Realität (18/21 MIWG, Compensation-, Boundary-, Error-/Escalation-, Subprozess- und Multi-Instance-Support). Diese Datei wird als veraltet gekennzeichnet / auf die aktuelle Supportmatrix und den MIWG-Bericht verwiesen, damit keine Selbstauskunft einen falschen „big gap&quot;-Eindruck erzeugt und keine widersprüchliche CI-Aussage stehen bleibt.

**Ehrliche Aussagegrenze (Abnahmekriterium):** Aus der endlichen Referenzsuite (21 MIWG-Fälle, TCK-Runner) wird **keine** Aussage „jede Kombination bewiesen&quot; abgeleitet. Die Zusagen gelten nur für die konkret geprüften, versionsgebundenen Pfade; offene Fälle (Subprozess-Boundary-Bewaffnung, gepinnter DMN-TCK-Testsatz) verhindern die **Voll**-Konformitätszusage in diesen Teilbereichen, nicht die enger abgegrenzte Produktfreigabe.

---

## Offene, ehrlich benannte Punkte

1. **Subprozess-Boundary-Bewaffnung:** Die persistente Runtime bewaffnet Boundary-Timer-Jobs über `CreateUserNodeAsync` (task-artige Knoten). Eine an einem **oder Subprozess** angehängte Boundary wird aktuell **nicht** als Job/Subscription bewaffnet (P7_AC_04 nutzt die unterstützte User-Task-Kombination). Kein Voll-`supported`-Claim für Subprozess-Boundary, bis dies engine-seitig ergänzt und getestet ist.
2. **DMN-TCK CI-Einbindung:** Testsatz ist gepinnt (`eng/dmn-tck.version`, Revision `20274cd2`), Runner und Skript vorhanden, lokaler Lauf 3391/3391 grün — aber der DMN-TCK-Schritt läuft noch nicht im CI-Workflow (nur out-of-band reproduzierbar). Die CI-Verdrahtung des gepinnten Skripts bleibt als Folgearbeit.
3. **Fehlender Engine-Change für Subprozess-Boundary** bleibt bewusst unangetastet (kein pauschales Umschreiben; nur gemessene/risikobehaftete Engpässe werden gefixt).

## Zuletzt bestätigte Testläufe (echte Infrastruktur)
- `Phase7ConformanceAcceptanceTests.*`: **Total 4, Errors 0, Failed 0, Skipped 0** (P7_AC_01/02/06, P7_AC_03, P7_AC_04, P7_AC_05) — gerechnet mit den drei API-Tests 3/3 und dem Doku-Nachweis.
- MIWG-Baseline: 18/21 Completed, 3/21 interactive-pending (Gate bilateral grün).
- **DMN-TCK (gepinnte Revision `20274cd2`, Level 2+3): 3391/3391 passed, 0 failed, Exit 0.**
