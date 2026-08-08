# VertexBPMN™

**Eine moderne, hochperformante BPMN 2.0, CMMN 1.1 & DMN 1.4 Engine für .NET**

![Build Status](https://img.shields.io/github/actions/workflow/status/VertexBPMN/VertexBPMN/build.yml?branch=main&style=for-the-badge)
![NuGet Version](https://img.shields.io/nuget/v/VertexBPMN.Core?style=for-the-badge)
![NuGet Downloads](https://img.shields.io/nuget/dt/VertexBPMN.Core?style=for-the-badge)
![License](https://img.shields.io/github/license/VertexBPMN/VertexBPMN?style=for-the-badge)
![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?style=for-the-badge&logo=dotnet)

---

**VertexBPMN™** ist eine von Grund auf neu entwickelte Prozess-Engine für das .NET-Ökosystem. Inspiriert von der Robustheit von Camunda, aber gebaut mit der vollen Kraft von modernem .NET und C# 13, um maximale Performance und eine erstklassige Entwicklererfahrung zu bieten. Unser Ziel ist es, eine leichtgewichtige, skalierbare und Cloud-native Lösung für die Orchestrierung von Geschäftsprozessen, Cases und Entscheidungen bereitzustellen.

## ✨ Key Features

### BPMN 2.0 - Business Process Model and Notation
* **Umfassende BPMN 2.0-Konformität:** Start-, End-, Intermediate-, Boundary-Events, Tasks, Gateways, (Multi-)Subprozesse, Event-Subprozesse, Sequence Flows und mehr.
* **Verschachtelte Subprozesse & Boundary Events:** Unterstützung für fortgeschrittene BPMN-Modelle und Token-Flows.
* **Edge-Case-Handling:** Robuste Fehlerbehandlung für ungültige Modelle, fehlende Events, unbekannte Tasks und komplexe Inputs.

### CMMN 1.1 - Case Management Model and Notation
* **Case Management Unterstützung:** Human Tasks, Process Tasks, Case Tasks, Milestones und Stages.
* **Sentries & Entry/Exit Criteria:** Dynamische Case-Ausführung basierend auf Bedingungen und Events.
* **Discretionary Items:** Unterstützung für Wissensarbeiter, um Ad-hoc-Tasks hinzuzufügen.
* **Case File Items:** Datengetriebene Case-Ausführung mit automatischer Zustandspropagierung.

### DMN 1.4 - Decision Model and Notation
* **Integrierte DMN 1.4-Engine:** Geschäftsentscheidungen mit DMN-Tabellen und FEEL, nahtlos in BusinessRuleTasks integriert.
* **Decision Requirements Diagrams:** Unterstützung für komplexe Entscheidungsabhängigkeiten.
* **FEEL Expression Language:** Vollständige Unterstützung der Friendly Enough Expression Language.

### Plattform & Integration
* **Gebaut für modernes .NET:** Modernste C#-Features, hohe Performance, geringe Allokationen, echte Asynchronität.
* **Nahtlose bpmn.io-Integration:** Volle Kompatibilität mit den `bpmn-js`, `dmn-js`, `cmmn-js` und `form-js` Toolkits.
* **Flexible APIs:** REST-API und gRPC-Schnittstelle für Microservice-Architekturen.
* **Skalierbarer Job-Executor:** Asynchrone Timer- und Hintergrundaufgaben.
* **Pluggable Persistence:** EF Core (PostgreSQL, SQL Server) und Erweiterbarkeit für andere Datenbanken.
* **Process Mining & Analytics:** Persistente Event-Analytics, REST-API für Reporting, Zeitreihen, Mandantenfilter und Metriken.
* **Security:** Rollenbasierte Authentifizierung für alle Analytics- und Reporting-Endpunkte.

## 🚀 Projektstatus

**VertexBPMN™ ist jetzt produktionsreif für BPMN 2.0-, CMMN 1.1- und DMN 1.4-Workflows mit robuster Testabdeckung, Edge-Case-Handling und moderner Architektur.**

Alle Kernfeatures, inklusive verschachtelter Subprozesse, Boundary Events, Case Management, DMN-Integration und Fehlerbehandlung, sind implementiert und durch umfangreiche Unit- und Integrationstests abgesichert. Die Engine ist bereit für produktive Workflows und kann flexibel erweitert werden.

Wir freuen uns weiterhin über Feedback und Beiträge aus der Community!

## 🔒 Security & Analytics

Alle Analytics- und Reporting-Endpunkte sind durch rollenbasierte Authentifizierung geschützt (`[Authorize]`).
Die Event-Analytics ist persistent, performant und mandantenfähig.

### Beispiel: Analytics-API (JWT erforderlich)

```http
GET /api/analytics/events
Authorization: Bearer <JWT>
```

**Weitere Endpunkte:**
- `/api/analytics/event-stats` – Event-Typ-Statistiken
- `/api/analytics/events/by-tenant/{tenantId}` – Mandantenfilter
- `/api/analytics/events/timeseries/{eventType}` – Zeitreihen
- `/api/analytics/metrics/process` – Prozessmetriken

Alle Endpunkte sind über Swagger/OpenAPI dokumentiert und testbar.

## 🏁 Getting Started (Quick Start)

Sobald eine erste Version auf NuGet verfügbar ist, können Sie die Engine ganz einfach zu Ihrem Projekt hinzufügen.

**1. Installation**

```bash
dotnet add package VertexBPMN.Core
```

**2. Ein einfacher Prozess**

Hier ist ein minimales Beispiel, wie man die Engine verwendet, um einen Prozess zu starten:

```csharp
using VertexBPMN.Core;
using VertexBPMN.Core.Process;

// 1. Definiere ein einfaches BPMN 2.0-Prozessmodell als XML-String
const string bpmnProcess = @"
<?xml version=""1.0"" encoding=""UTF-8""?>
<bpmn:definitions xmlns:bpmn=""http://www.omg.org/spec/BPMN/20100524/MODEL"" 
                  targetNamespace=""http://bpmn.io/schema/bpmn"">
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

## 🖥️ CLI, API & Studio Dashboard

VertexBPMN kann lokal wie eine Control-Plane-Anwendung über die Terminal-CLI bedient werden. Die CLI führt Engine-Kommandos aus und kann nach dem OpenClaw-Prinzip das API-Gateway und das Blazor-Studio gemeinsam starten.

### Dashboard aus der CLI öffnen

```powershell
dotnet run --project src/VertexBPMN.Cli -- dashboard
```

Der Befehl:

1. verwendet eine bereits laufende API oder startet `VertexBPMN.Api` lokal,
2. wartet auf den Health-Endpoint `/api/Health`,
3. startet `VertexBPMN.Studio`,
4. öffnet das Dashboard im Standardbrowser.

Die Standardadressen sind:

| Dienst | Adresse |
| --- | --- |
| API-Gateway | `http://localhost:51870/` |
| Blazor Studio | `http://localhost:5263/` |
| API Health | `http://localhost:51870/api/Health` |

Der Alias `studio` startet denselben Workflow. Für eine interaktive CLI-Sitzung:

```powershell
dotnet run --project src/VertexBPMN.Cli
```

Danach kann der Befehl direkt eingegeben werden:

```text
vertexbpmn> dashboard
```

Die Dashboard-Startparameter können in [`src/VertexBPMN.Cli/appsettings.json`](src/VertexBPMN.Cli/appsettings.json) oder über `VERTEXBPMN_`-Umgebungsvariablen angepasst werden. Dazu gehören Projektpfade, URLs, automatischer API-/Studio-Start, Browseröffnung und das Readiness-Timeout.

### Architektur

```text
VertexBPMN.Cli (TUI / Control Plane)
  |
  v
VertexBPMN.Api (Runtime Gateway / REST / SignalR)
  |
  v
VertexBPMN.Studio (Blazor Web Dashboard)
```

Das Studio ruft die API über HTTP auf. Dadurch greifen CLI, API und Dashboard auf denselben Runtime-Zustand zu, wenn persistente Engine-Datenbanken konfiguriert sind. Die CLI-Dokumentation mit allen Befehlen und Konfigurationsbeispielen befindet sich in [`src/VertexBPMN.Cli/README.md`](src/VertexBPMN.Cli/README.md). Details zur Dependency-Konfiguration, Registry und Priorität der Konfigurationsquellen stehen in [`docs/dependency-configuration.md`](docs/dependency-configuration.md).

**3. Ein einfacher Case (CMMN)**

```csharp
// Starte einen CMMN Case
var caseInstance = await engine.CaseService.StartCaseByKeyAsync("Case_CustomerOnboarding");

// Schließe einen Human Task im Case ab
await engine.CaseService.CompleteHumanTaskAsync(caseInstance.Id, "HumanTask_ReviewApplication");
```

## 📚 OpenAPI & bpmn.io Integration

VertexBPMN™ bietet eine vollständige OpenAPI/Swagger-Spezifikation (`openapi.json`) für die REST-API. Damit ist die Engine nahtlos kompatibel mit:

- **bpmn-js, dmn-js, cmmn-js, form-js** (bpmn.io)
- **Camunda Modeler**
- **Swagger UI, ReDoc, Postman**

**Wichtige Endpunkte:**
- `GET/PUT /api/process-definition/{id}/xml` (BPMN-XML)
- `GET/PUT /api/decision-definition/{key}/xml` (DMN-XML)
- `GET/PUT /api/case-definition/{key}/xml` (CMMN-XML)
- `GET/PUT /api/task/{id}/form-schema` (User-Task-Formulare)

**Dokumentation & Nutzung:**
- Siehe [`docs/openapi.md`](docs/openapi.md) für Details und Beispiele.
- Die OpenAPI-Datei wird bei jedem Build automatisch generiert und kann direkt in Postman, Swagger UI oder bpmn.io-Tools importiert werden.

## ☁️ Cloud-Native Exzellenz

VertexBPMN™ ist für Cloud, Container und moderne DevOps-Umgebungen gebaut:
- Health-/Liveness-/Readiness-Probes (`/api/health`)
- Prometheus/OpenTelemetry-Metriken (`/api/metrics`, `/api/metrics/prometheus`)
- Asynchrone Job-Engine (BackgroundService)
- Graceful Shutdown, Dockerfile, Kubernetes-Ready
- Live-Inspector-API für Visual Debugging und Analytics

**Details, Beispiele und Kubernetes-Deployment:**
Siehe [`docs/cloud-native.md`](docs/cloud-native.md)

## 🚀 Innovationen & Einzigartige Features

VertexBPMN™ bietet mehr als klassische BPMN/CMMN/DMN-Engines:
- Live-Inspector-API & Visual Debugger
- Feature Flags & experimentelle Features
- API-Hooks für Process Mining & Predictive Analytics
- High-Performance-Architektur für .NET

**Details und Beispiele:**
Siehe [`docs/features-innovation.md`](docs/features-innovation.md)

## 📊 Process Mining & Analytics Hooks

VertexBPMN™ ist vorbereitet für moderne Analytics- und Mining-Workflows:
- Event-Log- und Token-Log-Export (API-Design)
- Predictive Analytics & KI-Hooks (Feature Flag)
- Kompatibel mit Celonis, Camunda Optimize, Power BI, u.v.m.

**Details und API-Entwürfe:**
Siehe [`docs/process-mining-hooks.md`](docs/process-mining-hooks.md)

## 🛣️ Roadmap & Vision

Die nächsten Schritte und die langfristige Vision für VertexBPMN™ findest du in [`docs/roadmap.md`](docs/roadmap.md).

## 🤝 Wie man beitragen kann (How to Contribute)

Wir freuen uns über jede Hilfe! Egal ob Sie Fehler melden, Code beitragen oder die Dokumentation verbessern – Ihr Beitrag ist wertvoll.

1. Schauen Sie sich unsere **[Issues](https://github.com/VertexBPMN/VertexBPMN/issues)** an. Insbesondere die mit den Labels `good first issue` oder `help wanted` sind ein guter Startpunkt.
2. Forken Sie das Repository.
3. Erstellen Sie einen neuen Branch für Ihr Feature (`git checkout -b feature/AmazingFeature`).
4. Implementieren Sie Ihr Feature und schreiben Sie die notwendigen Tests.
5. Erstellen Sie einen Pull Request.

Bitte lesen Sie unsere `CONTRIBUTING.md`-Datei für detailliertere Richtlinien.

## 📄 Lizenz (License)

Dieses Projekt ist unter der **MIT-Lizenz** lizenziert. Weitere Informationen finden Sie in der `LICENSE`-Datei.

## 🙏 Danksagungen (Acknowledgments)

* Ein großes Dankeschön an das **Camunda**-Team für die Pionierarbeit im Bereich der Open-Source-BPMN-Engines.
* Danke an das Team von **bpmn.io** für die fantastischen JavaScript-Toolkits, die das Modellieren von Prozessen zu einer Freude machen.

---
*VertexBPMN™ ist eine nicht eingetragene Marke von Yovanny Rodríguez/Tainosoft UG.*
*VertexBPMN™ is an unregistered trademark of Yovanny Rodríguez/Tainosoft UG.*
