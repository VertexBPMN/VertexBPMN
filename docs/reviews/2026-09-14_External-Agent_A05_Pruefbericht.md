# A05 – Read-only Vertragsprüfer: Implementierungs- und Prüfbericht

Stand: 2026-09-14
Status: **Implementierung vollständig; reale Modell- und Fachabnahme ausstehend**

## Ergebnis

Der konkrete Anwendungsfall ist `agent.contract-review` mit dem lokalen,
anbieterneutral angebundenen Profil `contract-reviewer.v1`. `IAgentRuntime` besitzt genau
einen echten Adapter für Ollama. Der Adapter folgt dem offiziellen nicht streamenden
[`POST /api/chat`](https://docs.ollama.com/api/chat)-Vertrag; JSON-Schema-Ausgabe und
`num_predict` entsprechen der offiziellen API- beziehungsweise
[`Modelfile`](https://docs.ollama.com/modelfile)-Dokumentation.

Der bestehende `AIServiceTaskHandler` und dessen Mockpfad wurden weder ersetzt noch als
Abnahmenachweis verwendet.

## Erzwungene Grenzen

- `local-sensitive`: nur exakter Loopback-Endpunkt mit explizitem Port, keine Redirects,
  erneute Loopback-Prüfung aller DNS-Adressen beim Socketaufbau, kein Fallback.
- Das Modell erhält ausschließlich Manifest und Ergebnisse von `read_section` sowie
  literaler `search_document`-Suche im aktuellen Jobdokument.
- Keine Interfaces für Dateisystem, Shell, allgemeines HTTP, Credentials oder fremde Dokumente.
- Harte Limits für Dokument/Abschnitte/Suche/Tool- und Runtime-Calls/Laufzeit/Request-/Responsebytes,
  Outputtokens und Gesamttokens.
- Versioniertes Prompt `contract-reviewer.prompt.v1`; finales JSON-Schema mit festen Kategorien
  und Schweregraden.
- Dokumentversion, Abschnitt, Zeilenbereich und exaktes Zitat werden deterministisch geprüft.
  Nur ein Korrekturversuch ist erlaubt.
- `requiresHumanReview`, Promptversion, Dokumentversion und Dokument-Hash werden außerhalb des
  Modells gesetzt. Der Beispielprozess führt nach erfolgreicher Completion zwingend zu
  `human-review`.

## Lokale Verifikation

- `VertexBPMN.AgentWorker`, Release: 0 Fehler, 0 Warnungen.
- A05-Unit- und A04-Completion-Durchstich: 27 Tests bestanden, 0 fehlgeschlagen.
- E12: Prompt-Injection kann weder neue Tools noch einen Human-Review-Bypass erzeugen.
- E13: unbekannte Tools, erfundene Belege, falsche Dokumentversion, zu großes Dokument,
  ungültige Ausgabe und Tokenüberschreitung werden fail-closed behandelt.

## Noch nicht als bestanden gewertet

Auf dem Prüfhost waren weder ein `ollama`-Kommando noch eine Runtime unter
`127.0.0.1:11434` erreichbar. Deshalb wurde kein konkretes Modell stillschweigend ausgewählt
und kein Mock als Fachbenchmark gezählt. Die opt-in Abnahme
`ContractReviewOllamaAcceptanceTests` erwartet `VERTEXBPMN_TEST_OLLAMA_MODEL` und hält Modell,
Endpoint, Prompt-/Schema-Version sowie das synthetische Dokument im Test fest. A05 darf erst
nach einem grünen Lauf dieses Tests mit dem freizugebenden Modell als real abgenommen gelten.
