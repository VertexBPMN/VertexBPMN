# VertexBPMN

**Eine moderne, hochperformante BPMN 2.0 & DMN 1.4 Engine für .NET 9.**

![Build Status](https://img.shields.io/github/actions/workflow/status/DEIN-NAME/VertexBPMN/build.yml?branch=main&style=for-the-badge)
![NuGet Version](https://img.shields.io/nuget/v/VertexBPMN.Core?style=for-the-badge)
![NuGet Downloads](https://img.shields.io/nuget/dt/VertexBPMN.Core?style=for-the-badge)
![License](https://img.shields.io/github/license/DEIN-NAME/VertexBPMN?style=for-the-badge)

---

**VertexBPMN** ist eine von Grund auf neu entwickelte Prozess-Engine für das .NET-Ökosystem. Inspiriert von der Robustheit von Camunda, aber gebaut mit der vollen Kraft von .NET 9 und C# 13, um maximale Performance und eine erstklassige Entwicklererfahrung zu bieten. Unser Ziel ist es, eine leichtgewichtige, skalierbare und Cloud-native Lösung für die Orchestrierung von Geschäftsprozessen und Entscheidungen bereitzustellen.

## ✨ Key Features

* **Umfassende BPMN 2.0-Konformität:** Unterstützt alle wichtigen Elemente wie Events, Tasks, Gateways, Subprozesse und mehr.
* **Integrierte DMN 1.4-Engine:** Treffen Sie Geschäftsentscheidungen mit DMN-Tabellen und der FEEL-Sprache direkt in Ihren Prozessen.
* **Nahtlose bpmn.io-Integration:** Volle Kompatibilität mit den `bpmn-js`, `dmn-js` und `form-js` Toolkits für ein erstklassiges Modeling-Erlebnis.
* **Gebaut für .NET 9:** Nutzt modernste C#-Features für hohe Performance, geringe Allokationen und echte Asynchronität.
* **Flexible APIs:** Bietet sowohl eine REST-API für weitreichende Kompatibilität als auch eine gRPC-Schnittstelle für hochperformante Microservice-Kommunikation.
* **Skalierbarer Job-Executor:** Ein robuster Mechanismus für die asynchrone Ausführung von Timern und Hintergrundaufgaben.
* **Pluggable Persistence:** Standardmäßig mit EF Core für PostgreSQL & SQL Server, aber erweiterbar für andere Datenbanken.

## 🚀 Projektstatus

**VertexBPMN befindet sich derzeit in aktiver Entwicklung und ist noch nicht für den produktiven Einsatz bereit.**

Wir arbeiten aktiv an der Implementierung der Kernfeatures gemäß unserer Roadmap. Wir freuen uns über Feedback und Beiträge aus der Community!

## 🏁 Getting Started (Quick Start)

Sobald eine erste Version auf NuGet verfügbar ist, können Sie die Engine ganz einfach zu Ihrem Projekt hinzufügen.

**1. Installation**

```bash
dotnet add package VertexBPMN.Core
````

**2. Ein einfacher Prozess**

Hier ist ein minimales Beispiel, wie man die Engine verwendet, um einen Prozess zu starten:

```csharp
using VertexBPMN.Core;
using VertexBPMN.Core.Process;

// 1. Definiere ein einfaches BPMN 2.0-Prozessmodell als XML-String
const string bpmnProcess = @"
<?xml version=""1.0"" encoding=""UTF-8""?>
<bpmn:definitions xmlns:bpmn=""[http://www.omg.org/spec/BPMN/20100524/MODEL](http://www.omg.org/spec/BPMN/20100524/MODEL)"" 
                  targetNamespace=""[http://bpmn.io/schema/bpmn](http://bpmn.io/schema/bpmn)"">
  <bpmn:process id=""Process_HelloWorld"" isExecutable=""true"">
    <bpmn:startEvent id=""StartEvent_1""/>
  </bpmn:process>
</bpmn:definitions>";

// 2. Baue eine In-Memory-Engine für einen schnellen Test
var engine = await new EngineBuilder()
    .UseInMemoryStorage()
    .BuildAsync();

// 3. Deploye den Prozess in die Engine
var deployment = await engine.RepositoryService.DeployAsync(
    new ProcessResource(bpmnProcess, "hello-world.bpmn")
);

Console.WriteLine($"Prozess '{deployment.Name}' erfolgreich deployed.");

// 4. Starte eine neue Instanz des Prozesses
var processInstance = await engine.RuntimeService.StartProcessByKeyAsync("Process_HelloWorld");

Console.WriteLine($"Prozessinstanz mit der ID '{processInstance.Id}' wurde gestartet!");

// Output:
// Prozess 'hello-world.bpmn' erfolgreich deployed.
// Prozessinstanz mit der ID '...' wurde gestartet!
```

## 🗺️ Roadmap

Unsere Vision für VertexBPMN ist groß\! Wir folgen einem strukturierten Plan, der in mehrere Phasen unterteilt ist:

  * **Phase 1: Fundament & MVP:** Implementierung des Parsers, der Kern-Token-Engine und der grundlegenden Services.
  * **Phase 2: Feature-Vervollständigung & Konformität:** Umsetzung aller BPMN- & DMN-Features und Bestehen der offiziellen Test-Kits.
  * **Phase 3: Ökosystem & Härtung:** Fertigstellung der APIs, SDKs, Observability und Performance-Optimierung.
  * **Phase 4: Innovation:** Entwicklung einzigartiger Features wie dem visuellen Debugger und Predictive Analytics.

Weitere Details finden Sie in unserem `PROJEKTBOARD-LINK`.

## 🤝 Wie man beitragen kann (How to Contribute)

Wir freuen uns über jede Hilfe\! Egal ob Sie Fehler melden, Code beitragen oder die Dokumentation verbessern – Ihr Beitrag ist wertvoll.

1.  Schauen Sie sich unsere **`ISSUES-LINK`** an. Insbesondere die mit den Labels `good first issue` oder `help wanted` sind ein guter Startpunkt.
2.  Forken Sie das Repository.
3.  Erstellen Sie einen neuen Branch für Ihr Feature (`git checkout -b feature/AmazingFeature`).
4.  Implementieren Sie Ihr Feature und schreiben Sie die notwendigen Tests.
5.  Erstellen Sie einen Pull Request.

Bitte lesen Sie unsere `CONTRIBUTING.md`-Datei für detailliertere Richtlinien.

## 📄 Lizenz (License)

Dieses Projekt ist unter der **MIT-Lizenz** lizenziert. Weitere Informationen finden Sie in der `LICENSE`-Datei.

## 🙏 Danksagungen (Acknowledgments)

  * Ein großes Dankeschön an das **Camunda**-Team für die Pionierarbeit im Bereich der Open-Source-BPMN-Engines.
  * Danke an das Team von **bpmn.io** für die fantastischen JavaScript-Toolkits, die das Modellieren von Prozessen zu einer Freude machen.

<!-- end list -->

