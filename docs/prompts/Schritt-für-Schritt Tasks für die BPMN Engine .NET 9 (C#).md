Hier kommt die strukturierte **Schritt-für-Schritt-Aufgabenliste** für deinen Coding-Agent, abgestimmt auf den ultimativen Prompt und die Roadmap.

---

# 🚦 Schritt-für-Schritt Tasks für die BPMN Engine .NET 9 (C#)

---

## **Phase 1 – Core Engine Entwicklung (Monate 1–3)**

### 1.1 Projekt Setup

* Erstelle ein modular aufgebautes .NET 9 Projekt mit separaten Bibliotheken für:

  * BPMN Engine Core
  * DMN Engine
  * Form Handling
  * API Layer
  * Persistence Layer
  * Tests

### 1.2 BPMN 2.0 Kern-Engine

* Implementiere den BPMN XML Parser, kompatibel mit bpmn.io-Diagrammen.
* Baue die Process-Instance-Engine mit:

  * Start-, Intermediate-, Boundary-, End-Events (inkl. Timer, Signal, Message, Error, Escalation)
  * Gateways: Exclusive, Inclusive, Parallel, Event-based, Complex
  * Subprozesse (Embedded, Call Activities, Event Subprocesses)
  * Multi-Instance Aktivitäten (parallel & sequenziell)
  * Compensation & Transaction Subprocess

### 1.3 DMN 1.4 Engine

* Entwickle Parser für DMN XML mit FEEL-Auswertung.
* Integriere DMN-Ausführung in Prozess-Engine (Decision Tasks).

### 1.4 Form Handling

* Entwickle JSON Schema basiertes Form-System kompatibel mit bpmn.io Forms.
* Implementiere Validierung & Databinding.

### 1.5 Unit Tests & Beispielprozesse

* Schreibe Unit Tests für alle BPMN-Elemente und DMN-Entscheidungen.
* Implementiere Beispielprozesse aus Camunda-Dokumentation.

---

## **Phase 2 – API & Ökosystem (Monate 4–6)**

### 2.1 API-Implementierung

* REST API mit CRUD für Prozesse, Tasks, Events, DMN.
* gRPC API mit gleichen Funktionen.
* WebSocket Server für Event-Push.

### 2.2 SDK & CLI

* Entwickle SDKs für C#, JS/TS, Python, Go (nur Kern-API für Start, Stop, Query).
* CLI-Tool mit Deploy, Start, Stop, Inspect, Debug-Befehlen.

### 2.3 Persistenz Layer

* Adapter für EF Core (SQL Server, PostgreSQL), MongoDB und In-Memory.
* Event Sourcing Option implementieren.

### 2.4 Cluster Support

* Implementiere verteiltes Locking & Task Queue (Kafka, RabbitMQ).
* Teste Clusterbetrieb.

---

## **Phase 3 – Differenzierung & Erweiterungen (Monate 7–12)**

### 3.1 Native .NET Optimierungen

* Profilerstellung und Performance-Tuning (Span<T>, Pipelines).
* Plattformübergreifende Kompatibilität sicherstellen.

### 3.2 Zero-Config Cloud Mode

* Self-Hosting-Modus mit minimaler Konfiguration.
* Einfaches Web-UI für Monitoring & Administration (Grundfunktionen).

### 3.3 Visual Debugger

* UI-Komponente für Step-by-Step Debugging von Prozess-Instanzen im Diagramm.
* Live-Datenanzeige während der Ausführung.

### 3.4 Predictive Process Analytics

* ML-basierte Analyse (z.B. via ML.NET) zur Engpassvorhersage implementieren.

### 3.5 Versionless Prozessmigration

* Live-Aktualisierung von Prozessdefinitionen ohne Neustart implementieren.

### 3.6 Offline-Modus

* Lokale Prozessausführung mit Sync bei Netzwerkverbindung.

---

## **Qualität & Dokumentation**

* Automatisierte Tests (Unit, Integration, Load) in CI/CD Pipeline.
* API-Dokumentation (Swagger/OpenAPI).
* Entwickler-Dokumentation & Tutorials (Extensibility, Plugins, Scripting).
* Beispiele & Demo-Prozesse.

---

Wenn du möchtest, kann ich auch noch eine **Priorisierung mit Zeitplänen, Milestones und Metriken** erstellen, um den Fortschritt messbar zu machen.

Möchtest du das?
