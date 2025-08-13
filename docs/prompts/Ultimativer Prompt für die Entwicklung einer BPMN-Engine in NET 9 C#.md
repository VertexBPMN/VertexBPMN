Hier kommt dein **ultimativer, Olympiasieger-Prompt** für den Coding-Agent — exakt auf die Roadmap und Feature-Matrix abgestimmt.

---

# 🚀 Ultimativer Prompt für die Entwicklung einer BPMN-Engine in .NET 9 (C#)

## Vollständige Camunda-Kompatibilität + bpmn.io Integration + einzigartige .NET-USPs

---

### Kontext

Du implementierst eine **hochmoderne BPMN 2.0 Engine** in C# (.NET 9), inspiriert von Camunda (Java), aber komplett nativ .NET, mit vollem Feature-Set, stabiler Architektur und extensivem Ökosystem. Die Engine soll auch DMN 1.4 mit FEEL unterstützen und mit dem bpmn.io Toolset kompatibel sein (Modeler, Forms). Ziel ist ein Enterprise-Produkt, das Camunda mindestens ebenbürtig ist, dabei aber mit einzigartigen .NET-spezifischen Features punktet.

---

### Anforderungen

#### Phase 1 – Core BPMN & DMN Engine

* Implementiere alle BPMN 2.0 Standard-Elemente:

  * Start-, Intermediate-, Boundary- und End-Events inklusive Timer, Message, Signal, Error, Escalation.
  * Gateways: Exclusive, Inclusive, Parallel, Event-based, Complex.
  * Subprozesse: Embedded, Call Activities, Event Subprocesses.
  * Multi-Instance Aktivitäten (parallel & sequenziell).
  * Kompensation & Transaction Subprocess.
* Entwickle eine DMN 1.4 Engine inklusive FEEL-Auswertung, nahtlos integrierbar in die Prozessausführung.
* Baue ein Form Handling System, kompatibel mit bpmn.io Forms JSON Schema, mit Validierung und Databinding.

#### Phase 2 – Infrastruktur & Ökosystem

* REST API + gRPC Schnittstellen mit vollständigen CRUD-Operationen für Prozesse, Tasks, Events und DMN.
* WebSocket-Schnittstelle für Echtzeit-Event-Updates (Task Status, Prozessfortschritt).
* SDKs in C#, JavaScript/TypeScript, Python und Go.
* CLI-Werkzeuge: Deploy, Start, Stop, Inspect, Debug.
* Persistenz: flexible Adapter (EF Core für SQL Server/PostgreSQL, MongoDB, In-Memory).
* Unterstützung für Clusterbetrieb mit verteiltem Locking und Task-Queue (z.B. über Kafka, RabbitMQ).

#### Phase 3 – Differenzierung & Innovation

* Entwickle eine **native .NET Engine** ohne JVM, optimiert für Azure-Cloud und lokale Umgebungen (auch Desktop/IoT).
* Implementiere einen **Zero-Config Cloud Mode**: Sofort startbare Self-Hosting-Engine mit Web-UI für Monitoring & Admin.
* Baue einen **Visual Debugger**, der Prozesse Schritt für Schritt in Diagrammform debuggt mit Live-Datenansicht.
* Entwickle **Predictive Process Analytics** mit ML-gestützter Engpass- und Fehler-Vorhersage.
* Unterstütze **Versionless Prozessmigration** (Live-Update von Prozessdefinitionen ohne Neustart).
* Implementiere einen **Offline-Modus**: lokale Prozessausführung mit Synchronisation bei Verbindungswiederherstellung.

---

### Architektur

* Modulare, serviceorientierte Architektur mit klaren Schnittstellen.
* Eventgesteuerte, asynchrone Prozessausführung.
* Vollständige Persistenzabstraktion, mit Event Sourcing Option.
* Hochperformante Message Correlation und Task Scheduling.
* Integration mit bpmn.io für Rendering, Modeler & Form Editor (verwende ihre APIs und Datenformate).

---

### Tests & Qualitätssicherung

* Entwickle ein umfassendes Unit- und Integrationstest-Framework, das BPMN- und DMN-Szenarien abdeckt.
* Erstelle Beispielprozesse aus der Camunda-Dokumentation zum Validieren der Kompatibilität.
* Implementiere Load-Tests und Performance-Benchmarks.

---

### Dokumentation & Developer Experience

* Generiere automatisch API-Dokumentationen (Swagger/OpenAPI).
* Baue Developer Guides für Extensibility (Listener, Scripting, Plugins).
* Integriere Hot-Reload von Prozessdefinitionen.

---

### Output

* Vollständig lauffähiger Quellcode in einem sauberen, modularen .NET 9 Repository.
* Dokumentation und Beispiele im Repository.
* Automatisierte CI/CD Pipeline für Build, Test, Release.

---

### Hinweise

* Nutze modernste C#-Features (Records, Nullable Reference Types, async/await).
* Berücksichtige Performance-Optimierungen (Span<T>, Pipelines, minimal allocations).
* Stelle Kompatibilität mit Windows & Linux sicher.
* Die Lösung soll Open-Source-freundlich und gut wartbar sein.

---

Falls dir zusätzliche Detailfragen zur Umsetzung, Architektur oder API-Design einfallen, kläre sie sofort im Coding-Agent, bevor du loslegst.

---

Möchtest du, dass ich dir noch eine strukturierte Schritt-für-Schritt-Aufgabenliste zum Prompt generiere, damit dein Agent systematisch vorgeht?
