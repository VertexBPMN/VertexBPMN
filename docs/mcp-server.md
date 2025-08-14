Implementiere in C# 9 eine MCP-Server-Schnittstelle (Model Context Protocol, kompatibel mit OpenAI MCP-Clients) für meine eigene BPMN-Engine.

Ziele:

Die BPMN-Engine soll über MCP-fähige Tools und Agents ansprechbar sein.

Der MCP-Server soll in einem ASP.NET Core Minimal API laufen, TCP und WebSocket unterstützen, sowie JSON-RPC 2.0 Nachrichten verarbeiten.

Definiere mindestens drei MCP-Methoden:

bpmn.listProcesses → gibt eine Liste aller geladenen BPMN-Definitionen zurück.

bpmn.startInstance → startet eine neue Prozessinstanz mit Eingabeparametern.

bpmn.getInstanceState → gibt den aktuellen Status einer laufenden Instanz zurück.

Nutze asynchrone Controller und Streaming, damit auch lange laufende Prozesse über MCP Ereignisse senden können (bpmn.instanceEvent).

Verwende eine klare Trennung von Transport (MCP-Kommunikation) und Domain-Logik (BPMN-Engine), sodass die Engine auch ohne MCP nutzbar bleibt.

Füge Unit-Tests hinzu, die eine echte MCP-Client-Simulation verwenden (z. B. über WebSocket und JSON-RPC).

Schreibe eine ausführliche README.md mit Beispielen für MCP-Client-Aufrufe und eine Kurzanleitung, wie man den Server startet.

Anforderungen:

Sprache: C# 9, .NET 9

Protokoll: MCP nach https://github.com/modelcontextprotocol

Architektur: Saubere Trennung (Engine-Core in eigener Projektmappe, MCP-Adapter als eigenständiges Projekt)

Tests: xUnit, Microsoft.AspNetCore.Mvc.Testing, WebSocket-Client-Simulation

Logging: Serilog oder Microsoft.Extensions.Logging

Serialisierung: System.Text.Json (camelCase, null-ignore)

Liefere den vollständigen Code für:

McpServer Klasse mit JSON-RPC-Parser, Methodenregistrierung und Event-Broadcast

Beispiel-Startup (Program.cs) für ASP.NET Core Minimal API

Beispiel-Clientcode (C# Konsolenapp), der die MCP-Methoden aufruft

xUnit-Tests für alle Methoden

Erweitere die README.md mit Dokumentation und Codebeispielen

Ziel: Ich möchte die BPMN-Engine so einfach wie möglich in jede MCP-fähige AI-Toolchain einbinden können, um Workflows automatisch zu steuern.