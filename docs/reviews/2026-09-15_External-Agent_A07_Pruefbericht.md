# External Tasks und Agent-Vertragsprüfung – A07-Prüfbericht

Stand: 2026-09-16
Branch: `codex/external-agent-a00-inventory`
Status: **A07 durch den Auftraggeber am 2026-09-16 lokal abgenommen und als separate Phase übersprungen.** Technische Gesamtregression grün; die unten dokumentierten Grenzen der automatisierten E06-/E11- und Fachbenchmark-Nachweise bleiben transparent.

Die Abnahmeentscheidung beruht zusätzlich auf einem vom Auftraggeber selbst durchgeführten
lokalen Test. Sie ersetzt für diesen Lieferumfang die noch fehlenden automatisierten
Nachweise, wird aber nicht als grüner automatisierter E06-/E11- oder Fachbenchmark-Lauf
ausgegeben. Infrastrukturabhängige PostgreSQL-, Broker-, WSLC-, Keycloak- und Ollama-Tests
sind ausschließlich lokal auszuführen und werden im GitHub-Workflow explizit ausgeschlossen.

## Aktueller lokaler Nachweis

Der erste Diagnoselauf gegen die isolierte WSLC-PostgreSQL-Instanz auf Loopback-Port 55432
lieferte 267/268 bestandene Tests und einen Ollama-Skip. Danach wurden Ollama 0.34.0 und das
Modell `qwen3:8b` (Digest
`500a1f067a9f782620b40bee6f7b0c89e17ae61f686b92c24933e4ca4b2b8b41`,
Registry-Artefakt 5,2 GB) lokal installiert.

Der reale Modelltest deckte zwei Fehler im Produktionspfad auf und führte zu folgenden
Korrekturen:

- Der Ollama-Request setzt für Qwen3 explizit `think=false`. Der standardmäßig aktive
  Thinking-Modus verbrauchte das begrenzte Ausgabetokenbudget und endete mit HTTP 500.
- Der Adapter setzt `HttpClient.BaseAddress` nicht mehr bei jedem Aufruf. Stattdessen verwendet
  jeder Request eine absolute, bereits validierte URI; damit funktionieren mehrstufige
  Agent-Konversationen auch mit demselben Factory-Client.
- Ein neuer Unit-Test erzwingt die Wiederverwendbarkeit des Clients. Der Adapter-Vertragstest
  erzwingt zusätzlich `think=false`.

Der einzelne reale Ollama-Test bestand anschließend mit **1/1, 0 Skips** in 114,863 s.
Danach lief die vollständige lokale Auswahl mit PostgreSQL, realem Modell und
`-RequireNoSkips`:

- 269 Tests entdeckt
- 269 bestanden
- 0 fehlgeschlagen
- 0 übersprungen
- Laufzeit 193,035 s
- xUnit-Bericht:
  `tests/VertexBPMN.Tests/TestResults/external-tasks/fdb0c52392734815a4cadab8cf929af9/results.xml`

Dieser Lauf ist ein belastbarer technischer Regressionsnachweis des aktuellen Arbeitsstands.
Er ist noch keine fachliche A07-Abnahme, weil bislang nur ein synthetisches Dokument mit dem
realen Modell geprüft wurde.

Nach der Korrektur des Produktions-Feature-Gates bestand die abschließende deterministische
Auswahl für Deployment, Completion, Lease/Recovery, Worker, Agent-Handler, Operations-API,
Keycloak und AppHost mit **85/85 Tests, 0 Fehlern und 0 Skips** in 8,863 s. Der Release-Build
der Testassembly war erfolgreich; er enthält 31 bereits vorhandene Compiler-/Analyzerwarnungen.

## Gehärteter Abnahmerunner

`scripts/test-external-tasks-local.ps1` schreibt nun für jeden Lauf einen xUnit-XML-Bericht.
Mit `-RequireNoSkips` schlägt der Prozess fehl, sobald ein Pflichtfall übersprungen wurde oder
keine Tests entdeckt wurden. Damit kann der fehlende Modellnachweis nicht mehr durch einen
grünen Prozess-Exit verdeckt werden.

Abnahmeaufruf:

```powershell
$env:VERTEXBPMN_TEST_OLLAMA_MODEL = 'qwen3:8b'
./scripts/test-external-tasks-local.ps1 -NoBuild -RequireNoSkips
```

Modellname, Digest/Version, Ollama-Version, Hardware, Prompt- und Schemaversion,
Dokumentversionen, Laufzeiten und fachliche Messwerte müssen im finalen Bericht festgehalten
werden. Ein Modelllauf mit nur einem Dokument erfüllt den geforderten Fachbenchmark noch nicht.

## Versionierter Fachbenchmark

Unter `tests/VertexBPMN.Tests/TestData/ContractReviewBenchmark/v1` liegen nun:

- ein ausführbarer BPMN-Beispielprozess mit Agent-Vorprüfung, zwingendem Human-Review-Pfad und
  interruptierendem Timeout zum manuellen Recovery-Pfad;
- 20 synthetische, SHA-256-gebundene Dokumente mit klaren Klauseln, Widersprüchen,
  Deutsch/Englisch/Spanisch, Mehrabschnittsfällen und drei Prompt-Injection-Angriffen;
- vor dem Fachlauf festgelegte Schwellen für valide Ergebnisse, Kategorie-Recall/-Precision,
  kritischen Recall, Fundstellen, Human Review, Security-Verletzungen und p95-Laufzeit;
- ein maschinenlesbarer Benchmarktest mit JSON-Detailbericht sowie der lokale Runner
  `scripts/test-contract-review-benchmark-local.ps1`.

Der technische Manifesttest ist mit **1/1, 0 Skips** grün. Alle Dokumentversionen wurden gegen
den tatsächlichen UTF-8-SHA-256-Hash geprüft. Der Fachrunner wurde negativ verifiziert und bricht
vor der ersten Modellinferenz ab, solange `annotationStatus` nicht
`approved-domain-expert` ist und Name/Zeitpunkt der fachkundigen Freigabe fehlen. Die aktuelle
Annotation ist ausdrücklich `pending-domain-expert-review`; sie stammt nicht von einer
fachkundigen Vertragsprüferin oder einem Vertragsprüfer und darf nicht als fachliche Abnahme
ausgegeben werden.

## Akzeptierte Nachweisgrenzen

- Fachkundige Prüfung/Korrektur und signierte Freigabe der 20 technischen Referenzannotationen.
- Reale Ollama-Fachabnahme aller 20 Fälle gegen die bereits festgelegten Schwellen; die technische
  Ollama-/PostgreSQL-Gesamtregression ist ohne Skip grün.
- E06 benötigt weiterhin kontrollierte Hard-Crashes direkt an den Commitgrenzen.
- Der getrennte E11-Prozessrunner ist implementiert. Er startet echte API-/Worker-Prozesse,
  Keycloak, Ollama und eine dedizierte PostgreSQL-Instanz, beendet Worker/API hart und erhält
  die Datenbank über ein persistentes Testvolume. Auf diesem Rechner ist die Abnahme weiterhin
  blockiert: WSLC verliert veröffentlichte Ports nach Container-Neustarts und lieferte im letzten
  Lauf bereits beim ersten EF-Migrationszugriff `Timeout during reading attempt`. E11 bleibt
  Lauf deshalb nicht als automatisiert bestanden dokumentiert. Der Auftraggeber hat den
  Anwendungsfall separat lokal getestet und für diesen Lieferumfang abgenommen.
- Finale Regression aus einem sauberen Checkout des freigegebenen Commits.
- Betriebsrunbook ist jetzt unter `docs/runbooks/external-agent-contract-review.md` vorhanden;
  seine tatsächliche Recovery-Prozedur muss noch zusammen mit E06/E11 im getrennten Prozesslauf
  verifiziert werden.

Die konkrete Zuordnung ist im [E01-E14-Nachweis](2026-09-15_External-Agent_A07_E01-E14_Matrix.md)
dokumentiert. Die fehlenden automatisierten E06-/E11- und Fachbenchmark-Nachweise sind dort
als akzeptierte Restrisiken sichtbar; sie blockieren nach der lokalen Auftraggeberabnahme A07
für diesen Lieferumfang nicht mehr.
