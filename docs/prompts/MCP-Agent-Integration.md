Du bist ein Senior C#-Architekt.
Erweitere meine bestehende, nahezu vollständige BPMN-Engine in .NET 9 C#, um MCP-Agent-Integration zu unterstützen, sodass die Engine MCP-Agents über eine REST- oder WebSocket-Schnittstelle orchestrieren kann.
Wichtiger Hinweis: Die BPMN-Engine-Core-Logik (Parsing, TokenEngine, ExecutionContext, BPMN 2.0 Support) ist bereits vorhanden. Du sollst nur die neuen Komponenten ergänzen, die die Integration ermöglichen.

Vorhandene Engine-Funktionen:

Unterstützt Service Task, Send Task, Receive Task, Parallel Gateway, Exclusive Gateway, Timer Event, Error Event.

Verarbeitet BPMN-Definitionen aus XML (BPMN 2.0 konform).

Nutzt ExecutionContext-Objekt für Variablen und Prozessstatus.

Neue Anforderungen (MCP-Agent Integration):

Service Task → MCP-Agent Call:

Jeder Service Task kann einen konfigurierten MCP-Agent aufrufen.

Kommunikation über REST (HttpClient) und optional WebSocket.

Input-Mapping: BPMN-Prozessvariablen → MCP-Request-Payload.

Output-Mapping: MCP-Response → BPMN-Prozessvariablen.

Asynchronität:

Unterstützung für synchrone und asynchrone Aufrufe (Receive Task + Correlation).

async/await nutzen, keine Blockierung bei lang laufenden Tasks.

Möglichkeit, nach Agent-Antwort über ein Message Event fortzufahren.

Fehlerbehandlung:

Retry-Mechanismus mit konfigurierbarer Max-Retry-Anzahl.

Exception-Handling mit Logging.

Erweiterbarkeit:

Agent-Aufruf in einer separaten Klasse McpAgentService gekapselt:

csharp
Kopieren
Bearbeiten
Task<JObject> CallAgentAsync(string agentName, JObject input, CancellationToken ct)
Task<JObject> WaitForAgentResponseAsync(string correlationId, CancellationToken ct)
Neue Agents können per Konfiguration (JSON-Datei) hinzugefügt werden.

Beispiel-Workflow:

Beispielprozess-Datei (.bpmn) implementieren:
Start Event → Service Task "Analyse" (MCP-Agent NLP) → Exclusive Gateway:

Positiv → Service Task "Empfehlung" (MCP-Agent Recommender) → End Event

Negativ → Service Task "Review" (MCP-Agent Human-In-Loop) → End Event

Engine soll diesen Prozess laden und ausführen können.

Lieferumfang:

McpAgentService-Implementierung mit REST + optional WebSocket.

Anpassung der Engine, damit Service Tasks MCP-Calls ausführen können.

Beispiel .bpmn-Datei mit oben genanntem Workflow.

Vollständig lauffähige Konsolen-App (Program.cs) mit Demo-Ausführung.

Unit-Tests (xUnit) für Agent-Aufrufe und Output-Mapping.

README.md mit kurzer Dokumentation, wie MCP-Agents in der Engine genutzt werden.

XML-Kommentare für alle neuen Klassen und Methoden.

Ziel: Eine erweiterte BPMN-Engine, die MCP-Agents als verteilte Worker orchestrieren kann, ohne den bestehenden BPMN-Core-Code zu verändern.