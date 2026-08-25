# VertexBPMN™

**Eine moderne Prozessautomatisierungsplattform für .NET mit einem BPMN-Kern und experimentellen DMN-/CMMN-Modulen**

![Build Status](https://img.shields.io/github/actions/workflow/status/VertexBPMN/VertexBPMN/ci.yml?branch=master&style=for-the-badge)
![NuGet Version](https://img.shields.io/nuget/v/VertexBPMN.Sdk?style=for-the-badge)
![NuGet Downloads](https://img.shields.io/nuget/dt/VertexBPMN.Sdk?style=for-the-badge)
![License](https://img.shields.io/github/license/VertexBPMN/VertexBPMN?style=for-the-badge)
![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?style=for-the-badge&logo=dotnet)

---

**VertexBPMN™** ist eine von Grund auf neu entwickelte Prozess-Engine für das .NET-Ökosystem. Inspiriert von der Robustheit von Camunda, aber gebaut mit der vollen Kraft von modernem .NET und C# 13, um maximale Performance und eine erstklassige Entwicklererfahrung zu bieten. Unser Ziel ist es, eine leichtgewichtige, skalierbare und Cloud-native Lösung für die Orchestrierung von Geschäftsprozessen, Cases und Entscheidungen bereitzustellen.

## ✨ Key Features

### BPMN 2.0 - Business Process Model and Notation
* **Parser und Roundtrip:** Breite Modellabdeckung mit automatisierten Parser-, Serialisierungs- und MIWG-Referenztests.
* **Persistenter Runtime-Subset:** None Events, Service/User Tasks, Parallel Gateway, Timer Catch/Boundary, Message/Signal und ein begrenzter Kompensationspfad laufen über denselben relationalen API-Runtime-Pfad.
* **Klare Grenze:** Vollständige BPMN-Konformität, komplexe Gateways, Event-Subprozesse, Call Activities und echte Multi-Instance-Semantik werden noch nicht behauptet.

### CMMN 1.1 - Case Management Model and Notation
* **Experimentell:** Parser, Modelle und einzelne API-Verträge sind vorhanden.
* **Nicht als produktionsreif freigegeben:** Vollständige Sentry-, Discretionary-Item- und Case-Lifecycle-Semantik ist nicht durch eine Conformance-Suite belegt.

### DMN - Decision Model and Notation
* **Teilweise unterstützt:** Decision Tables und mehrere Hit Policies besitzen automatisierte Tests.
* **Nicht vollständig:** FEEL, DRD und die DMN-Konformität sind nicht vollständig implementiert oder durch die offizielle TCK nachgewiesen.

### Plattform & Integration
* **Gebaut für modernes .NET:** Modernste C#-Features, hohe Performance, geringe Allokationen, echte Asynchronität.
* **bpmn.io-Integration:** Gepinnte Web-Assets und eigene Moddle-Verifikation; vollständige Interoperabilität wird noch nicht behauptet.
* **Flexible APIs:** REST-API und gRPC-Schnittstelle für Microservice-Architekturen.
* **Persistenter Job-Executor:** Timer-Jobs besitzen Lease, Retry/Backoff, Dead Letter und Incident-Anbindung.
* **Konfigurierbare Persistenz:** EF Core für SQLite, PostgreSQL und SQL Server; der freigegebene BPMN-Runtime-Subset speichert Tokens, Variablen, Tasks, Jobs, Subscriptions, Incidents sowie Inbox/Outbox dauerhaft.
* **Process Mining & Analytics (teilweise):** Persistente Events und REST-Abfragen sind vorhanden; atomare Runtime-Kopplung und vollständige Betriebsmetriken fehlen.
* **Security:** Rollenbasierte Authentifizierung für alle Analytics- und Reporting-Endpunkte.

## 🚀 Projektstatus

**VertexBPMN™ ist derzeit nicht als vollständig produktionsreife BPMN-, DMN- oder CMMN-Engine freigegeben.**

Phase 2 stellt einen begrenzten, persistenten BPMN-Produktionskern bereit; die Gesamtplattform ist wegen noch offener Betriebs-, Broker-, Deployment-, Security-Gate-, DMN- und CMMN-Arbeiten weiterhin nicht vollständig produktionsreif. Der tatsächliche Supportstatus, bekannte Einschränkungen und verbindliche Akzeptanzfälle stehen in der [Produkt-Support- und Acceptance-Matrix](docs/reference/product-support-matrix.md). Funktionen ohne bestandenen End-to-End-Nachweis gelten als `partial` oder `unsupported`, auch wenn Parser-, Unit- oder Komponenten-Tests existieren.

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

Die öffentliche .NET-Integration von VertexBPMN erfolgt über das NuGet-Paket **VertexBPMN.Sdk**. Das SDK kommuniziert mit einer laufenden VertexBPMN-API und bietet typisierte Methoden für Deployment, Prozessstart, Instanzen und Workflow-Trigger.

**1. SDK installieren**

```bash
dotnet add package VertexBPMN.Sdk
```

**2. API-Client konfigurieren**

Die API muss erreichbar sein. Für geschützte Endpunkte wird ein gültiger Bearer-Token oder API-Key benötigt:

```csharp
using VertexBPMN.Sdk;

using var httpClient = new HttpClient
{
    BaseAddress = new Uri("http://localhost:51870/")
};

var client = new VertexBpmnClient(
    httpClient,
    new VertexBpmnClientOptions
    {
        BearerToken = Environment.GetEnvironmentVariable("VERTEXBPMN_BEARER_TOKEN"),
        TenantId = "acme"
    });
```

**3. BPMN deployen und Prozess starten**

```csharp
const string bpmnXml =
    "<definitions xmlns=\"http://www.omg.org/spec/BPMN/20100524/MODEL\">" +
    "<process id=\"Process_HelloWorld\">" +
    "<startEvent id=\"start\" />" +
    "<endEvent id=\"end\" />" +
    "</process>" +
    "</definitions>";

var deployed = await client.DeployProcessAsync(
    bpmnXml,
    "hello-world.bpmn");

if (deployed is null)
    throw new InvalidOperationException("Das BPMN-Deployment wurde nicht zurückgegeben.");

var processInstance = await client.StartProcessAsync(
    deployed.Key,
    new Dictionary<string, object?>
    {
        ["source"] = "quick-start"
    },
    businessKey: "HELLO-001");

Console.WriteLine($"Prozess '{deployed.Key}' wurde deployt.");
Console.WriteLine($"Prozessinstanz '{processInstance?.Id}' wurde gestartet.");
```

Für externe Starts kann anschließend ein Workflow-Trigger registriert und über sein einmalig ausgegebenes Secret aufgerufen werden. Die vollständige Anleitung steht im Abschnitt [Persistente BPMN-Deployments und externe Workflow-Trigger](#persistente-bpmn-deployments-und-externe-workflow-trigger) sowie in [docs/runbooks/workflow-triggers.md](docs/runbooks/workflow-triggers.md).

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

### Persistente BPMN-Deployments und externe Workflow-Trigger

BPMN-Workflows können jetzt dauerhaft im Repository registriert und später manuell oder durch externe Systeme gestartet werden. Der typische Ablauf ist:

1. BPMN-Datei persistent deployen.
2. Einen tenantbezogenen Workflow-Trigger für den Process-Key registrieren.
3. Das einmalig ausgegebene Secret sicher speichern.
4. Den Trigger über API, CLI, SDK oder Studio aufrufen.

#### REST API

Ein BPMN-Workflow wird über das Repository deployt:

```http
POST /api/repository
Authorization: Bearer <JWT>
Content-Type: application/json

{
  "bpmnXml": "<definitions>...</definitions>",
  "name": "order-process.bpmn",
  "tenantId": "acme"
}
```

Danach kann ein geschützter Trigger angelegt werden:

```http
POST /api/triggers
Authorization: Bearer <JWT>
Content-Type: application/json

{
  "name": "Order webhook",
  "processDefinitionKey": "order-process",
  "tenantId": "acme"
}
```

Die Antwort enthält das Secret nur einmal. Persistiert wird ausschließlich ein Hash. Der externe Aufruf benötigt keine Benutzeranmeldung, aber das Trigger-Secret:

```http
POST /api/triggers/{id}/invoke
X-VertexBPMN-Trigger-Secret: <secret>
Content-Type: application/json

{
  "businessKey": "ORDER-123",
  "variables": {
    "customerId": "C-42"
  }
}
```

Weitere Verwaltungsendpunkte:

- `GET /api/triggers` – Trigger auflisten
- `PUT /api/triggers/{id}` – Trigger umbenennen oder aktivieren/deaktivieren
- `DELETE /api/triggers/{id}` – Trigger löschen

Die Verwaltung ist authentifiziert und tenantisoliert. Einzelheiten stehen in [docs/runbooks/workflow-triggers.md](docs/runbooks/workflow-triggers.md).

Credentials für Connectoren werden über `api/credentials` verwaltet. Die API gibt ausschließlich Metadaten und Secret-Key-Namen zurück; Klartext-Secrets bleiben serverseitig geschützt und werden auch bei Rotation nicht in Responses, Logs oder Audit-Details ausgegeben.

#### CLI

Für persistente BPMN-Deployments und Trigger stehen folgende Befehle zur Verfügung:

```text
deploy-bpmn <bpmn-file> [tenant]
trigger create <name> <process-key> [tenant]
trigger list [tenant]
trigger invoke <id> <secret> [variables-json] [business-key]
trigger enable|disable <id> [tenant]
trigger delete <id> [tenant]
```

Beispiel:

```powershell
dotnet run --project src/VertexBPMN.Cli -- deploy-bpmn examples/order.bpmn acme
dotnet run --project src/VertexBPMN.Cli -- trigger create "Order webhook" order-process acme
```

`register-bpmn` bleibt als lokaler Engine-Registrierungsbefehl für direkte CLI-Ausführung bestehen. Für dauerhaft gespeicherte BPMN-Definitionen, die später über API oder Trigger gestartet werden sollen, wird `deploy-bpmn` verwendet.

#### .NET SDK

```csharp
var deployed = await client.DeployProcessAsync(
    bpmnXml,
    "order-process.bpmn",
    "acme");

var created = await client.CreateWorkflowTriggerAsync(
    "Order webhook",
    "order-process",
    "acme");

// Das Secret sicher speichern; es wird nur bei der Registrierung zurückgegeben.
var instance = await client.InvokeWorkflowTriggerAsync(
    created!.Trigger.Id,
    created.Secret,
    new Dictionary<string, object?>
    {
        ["customerId"] = "C-42"
    },
    "ORDER-123");
```

#### Studio

Im Studio können BPMN-Dateien unter **Deployments** hochgeladen und tenantbezogen dauerhaft registriert werden. Unter **Workflow Triggers** können anschließend Trigger erstellt, aktiviert/deaktiviert, getestet und gelöscht werden. Das Secret wird nach der Erstellung einmalig angezeigt.

### SDK-NuGet-Release

Das SDK wird durch GitHub Actions als NuGet-Paket veröffentlicht. Der Workflow verwendet [NuGet Trusted Publishing](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing) und keine dauerhaft gespeicherte API-Key-Secret.

Einmalige Einrichtung:

1. Auf nuget.org unter **Trusted Publishing** eine GitHub-Actions-Policy für dieses Repository anlegen:
   - Repository Owner: `VertexBPMN`
   - Repository: `VertexBPMN`
   - Workflow File: `ci.yml` (nur der Dateiname, nicht der Pfad)
2. Im GitHub-Repository das Actions-Secret `NUGET_USER` mit dem NuGet-Profilnamen hinterlegen. Für dieses Repository ist das `yrodriguez`; nicht die E-Mail-Adresse verwenden.
3. Einen SemVer-Tag erstellen und pushen, zum Beispiel:

   ```bash
   git tag v1.0.1
   git push origin v1.0.1
   ```

Der Workflow baut und testet die Solution, packt `VertexBPMN.Sdk`, tauscht das GitHub-OIDC-Token gegen einen kurzlebigen NuGet-Veröffentlichungsschlüssel und veröffentlicht anschließend `VertexBPMN.Sdk.1.0.1.nupkg` auf NuGet.org. Jeder normale CI-Lauf erzeugt zusätzlich ein herunterladbares SDK-NuGet-Artefakt, veröffentlicht es aber nicht.

Verifikation: Nach dem einmaligen Einrichten der NuGet-Policy einen neuen, noch nicht verwendeten SemVer-Tag pushen. Der Job **Publish VertexBPMN.Sdk to NuGet** muss erfolgreich sein und das Paket anschließend unter `https://www.nuget.org/packages/VertexBPMN.Sdk/<version>` erreichbar sein. Ein fehlgeschlagener OIDC-Login weist auf eine abweichende Repository-, Workflow-Datei- oder NuGet-Benutzerkonfiguration hin; dafür wird kein API-Key benötigt.

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

Das Studio ruft die API über HTTP auf. Dadurch greifen CLI, API und Dashboard auf denselben Runtime-Zustand zu, wenn persistente Engine-Datenbanken konfiguriert sind. Die CLI-Dokumentation mit allen Befehlen und Konfigurationsbeispielen befindet sich in [`src/VertexBPMN.Cli/README.md`](src/VertexBPMN.Cli/README.md). Details zur Dependency-Konfiguration, Registry und Priorität der Konfigurationsquellen stehen in [`docs/runbooks/dependency-configuration.md`](docs/runbooks/dependency-configuration.md).

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
- Siehe [`docs/reference/openapi.md`](docs/reference/openapi.md) für Details und Beispiele.
- Die OpenAPI-Datei wird bei jedem Build automatisch generiert und kann direkt in Postman, Swagger UI oder bpmn.io-Tools importiert werden.

## ☁️ Cloud-Native Exzellenz

VertexBPMN™ ist für Cloud, Container und moderne DevOps-Umgebungen gebaut:
- Health-/Liveness-/Readiness-Probes (`/api/health`)
- Prometheus/OpenTelemetry-Metriken (`/api/metrics`, `/api/metrics/prometheus`)
- Asynchrone Job-Engine (BackgroundService)
- Graceful Shutdown, Dockerfile, Kubernetes-Ready
- Live-Inspector-API für Visual Debugging und Analytics

**Details, Beispiele und Kubernetes-Deployment:**
Siehe [`docs/runbooks/cloud-native.md`](docs/runbooks/cloud-native.md)

## 🚀 Innovationen & Einzigartige Features

VertexBPMN™ bietet mehr als klassische BPMN/CMMN/DMN-Engines:
- Live-Inspector-API & Visual Debugger
- Feature Flags & experimentelle Features
- API-Hooks für Process Mining & Predictive Analytics
- High-Performance-Architektur für .NET

**Details und Beispiele:**
Siehe [`docs/architecture/features-innovation.md`](docs/architecture/features-innovation.md)

## 📊 Process Mining & Analytics Hooks

VertexBPMN™ ist vorbereitet für moderne Analytics- und Mining-Workflows:
- Event-Log- und Token-Log-Export (API-Design)
- Predictive Analytics & KI-Hooks (Feature Flag)
- Kompatibel mit Celonis, Camunda Optimize, Power BI, u.v.m.

**Details und API-Entwürfe:**
Siehe [`docs/reference/process-mining-hooks.md`](docs/reference/process-mining-hooks.md)

## 🛣️ Roadmap & Vision

Die nächsten Schritte und die langfristige Vision für VertexBPMN™ findest du in [`docs/working/roadmap.md`](docs/working/roadmap.md).

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
