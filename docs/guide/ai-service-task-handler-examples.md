# KI- und Agenten-Integration

VertexBPMN integriert externe KI als BPMN-Service-Task, über MCP oder als separaten External Task Worker. Die Prozesssteuerung bleibt explizit im BPMN-Modell. Es gibt derzeit keinen eingebauten Chat-/Copilot-Assistenten im Studio und kein mitgeliefertes LLM.

## AI-Service-Task

Ein BPMN-Service-Task kann mit dem Typ `aiServiceTask` registriert werden. Der Providerpfad des `AIServiceTaskHandler` verarbeitet OpenAI, Anthropic und Google Gemini. Zugangsdaten und Modelle müssen vom Betreiber passend zum Anbieter eingerichtet werden.

```xml
<serviceTask id="classify-request" name="Anfrage klassifizieren">
  <extensionElements>
    <zeebe:taskDefinition type="aiServiceTask" />
    <zeebe:taskHeaders>
      <zeebe:header key="ai:provider" value="openai" />
      <zeebe:header key="ai:model" value="&lt;provider-model-id&gt;" />
      <zeebe:header key="ai:prompt" value="Ordne die Anfrage einer Kategorie zu." />
      <zeebe:header key="ai:inputVariables" value="requestText" />
      <zeebe:header key="ai:resultVariable" value="requestCategory" />
      <zeebe:header key="ai:temperature" value="0" />
      <zeebe:header key="ai:maxTokens" value="200" />
    </zeebe:taskHeaders>
  </extensionElements>
</serviceTask>
```

Der Handler kann außerdem `ai:systemMessage`, `ai:timeout`, `ai:retryCount`, `ai:includeMetadata`, `ai:contextEnrichment`, `ai:mcpIntegration`, `ai:mcpServerUrl` und `ai:mcpMethod` auswerten. Das Ergebnis landet in `ai:resultVariable` (Standard: `aiResult`); bei Fehlern werden `aiTask_error` und `aiTask_failed` gesetzt und der Service-Task schlägt fehl.

### Provider und Schlüssel

| Provider | Wert von `ai:provider` | Schlüsselname |
| --- | --- | --- |
| OpenAI | `openai` | `OPENAI_API_KEY` oder konfigurierte `ApiKeyEnvironmentVariable` |
| Anthropic | `anthropic` | `ANTHROPIC_API_KEY` oder konfigurierte `ApiKeyEnvironmentVariable` |
| Google Gemini | `gemini` oder `google` | `GEMINI_API_KEY` oder `GOOGLE_API_KEY` |

Die universelle `AIServiceTaskHandler`-Konfiguration kann Provider-Modelle unter `Dependencies:Ai:Models` definieren, einschließlich `Provider`, `Model`, `Endpoint` und `ApiKeyEnvironmentVariable`; sie liest den Schlüssel aus der angegebenen Umgebungsvariable. Die separaten OpenAI-/Anthropic-Handler unterstützen zusätzlich den `ISecretProvider`. Keine API-Schlüssel in BPMN-XML, Repository-Dateien oder Prozessvariablen eintragen. Provider-Modellnamen und API-Verträge ändern sich; einen für das eigene Anbieterkonto gültigen Modellbezeichner verwenden.

### Grenzen der generischen Aliase

Die Aliase `ai:generic`, `ai:cohere`, `ai:huggingface`, `ai:ollama`, `ai:local` und `ai:custom` sind derzeit dem `GenericAiServiceTaskHandler` zugeordnet. Dieser Handler liefert aktuell nur einen lokalen Platzhaltertext und ruft keinen Modellanbieter auf. Diese Aliase sind daher keine produktiven Providerintegrationen. `mock`/`test`-Modi sind nur für Tests und Entwicklung gedacht.

Die Provider-Tests simulieren HTTP-Antworten. Sie belegen weder Live-Kompatibilität mit einem Anbieter noch Konto-, Modell-, Rate-Limit- oder Verfügbarkeitsverhalten.

## MCP und KI-Agenten

MCP kann KI-Clients beziehungsweise Agenten mit VertexBPMN-Funktionen verbinden. Ein BPMN-`mcpServiceTask` sendet eine JSON-RPC-Anfrage an den in `mcpServerUrl` angegebenen Endpunkt und verwendet `mcpMethod`; Prozessvariablen werden als Parameter mitgegeben und ein erfolgreiches Ergebnis wird zurück in Variablen geschrieben. Der MCP-Endpunkt und seine Authentifizierung müssen separat bereitgestellt und abgesichert werden.

`ai:mcpIntegration=true` kann nach dem Provideraufruf eine konfigurierte MCP-Aktion auslösen. Das ist kein autonomer, unbeschränkter Agenten-Loop: Das BPMN-Modell kontrolliert Reihenfolge und erlaubte Prozessschritte.

## Lokaler Ollama-External-Task-Worker

`VertexBPMN.AgentWorker` enthält einen Contract-Review-Handler für einen Ollama-kompatiblen lokalen Modellserver. Er ist über `ContractReviewer:Enabled` standardmäßig deaktiviert. Bei Aktivierung verlangt die Konfiguration ein Modell und einen Loopback-Endpunkt; Dokumentgröße, Abschnitte, Tool-Aufrufe, Laufzeit, Antwortgröße und Tokenbudget sind begrenzt.

Der Reviewer darf nur Dokumentabschnitte lesen und literal durchsuchen. Er muss Befunde mit Zitaten und Zeilenbereichen ausgeben und darf einen Vertrag nicht genehmigen oder ablehnen. Die Entscheidung bleibt bei einem Menschen. Siehe [External Tasks und Agent-Vertragsprüfung](../runbooks/external-agent-contract-review.md).

## Daten- und Sicherheitsgrenzen

- Prompts und ausgewählte Prozessvariablen können an einen externen Modellanbieter übertragen werden. Datenklassifikation und Datenschutz vor Aktivierung prüfen.
- Credentials nur über Secret-/Environment-Konfiguration bereitstellen; Logs und Traces auf sensible Prompts und Antworten kontrollieren.
- MCP-Werkzeuge und Agenten mit minimalen Berechtigungen, authentifizierten Endpunkten und expliziten Allow-Lists betreiben.
- Modellantworten sind nicht vertrauenswürdige Eingaben. Vor kritischen Aktionen müssen BPMN-Validierung und gegebenenfalls menschliche Freigabe stehen.
