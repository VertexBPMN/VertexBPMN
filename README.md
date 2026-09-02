# VertexBPMN™

**Cloud-native Prozessautomatisierung für .NET mit BPMN 2.0, DMN, CMMN, Low-Code Studio, REST, gRPC, MCP, SDK und CLI.**

![Build Status](https://img.shields.io/github/actions/workflow/status/VertexBPMN/VertexBPMN/ci.yml?branch=master&style=for-the-badge)
![NuGet Version](https://img.shields.io/nuget/v/VertexBPMN.Sdk?style=for-the-badge)
![NuGet Downloads](https://img.shields.io/nuget/dt/VertexBPMN.Sdk?style=for-the-badge)
![License](https://img.shields.io/github/license/VertexBPMN/VertexBPMN?style=for-the-badge)
![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?style=for-the-badge&logo=dotnet)

VertexBPMN ist eine Open-Source-Plattform für die Modellierung, Ausführung und den Betrieb von Geschäftsprozessen, Entscheidungen und Cases. Die Plattform ist nativ für .NET 10 entwickelt und verbindet eine persistente Workflow-Runtime mit einem browserbasierten Studio, mandantenfähigen APIs, SDK, CLI, Messaging, Analytics und einer Aspire-basierten lokalen Entwicklungsumgebung.

## Supportstatus

Die verbindliche Quelle für Produkt-Support und Acceptance-Nachweise ist die [Produkt-Support- und Acceptance-Matrix](docs/reference/product-support-matrix.md). Alle dort aufgeführten Fähigkeiten sind als `supported` qualifiziert. `Supported` bedeutet: Die Funktion besitzt einen öffentlich nutzbaren, persistenten End-to-End-Pfad und ist durch die zugeordneten Acceptance- und Release-Gates abgesichert. Ein isolierter Parser- oder Unit-Test genügt dafür nicht.

Die Full-Product-Support-Suite umfasst 51 konkrete Acceptance-Fälle: 47 reguläre Verträge sowie vier externe RabbitMQ-/PostgreSQL-Verträge. Der schnelle GitHub-Workflow blockiert Pull Requests und Releases auf Restore, Release-Build und dem zentralen Testprojekt; die externen Infrastruktur-, Browser-, Benchmark- und erweiterten Security-Prüfungen bleiben für gezielte Qualifikationsläufe verfügbar.

### Standards und Runtime

| Bereich | Unterstützte Fähigkeiten | Status |
| --- | --- | --- |
| BPMN-Definitionen | Sichere XML-Verarbeitung, Validierung, Roundtrip, Deployment und tenantbezogene Versionierung | ✅ Supported |
| BPMN-Basisfluss | None Start/End Events, Sequence Flows, Service Tasks und User Tasks mit Claim/Complete/Resume | ✅ Supported |
| BPMN-Gateways | Parallel, Exclusive, Inclusive, Event-based und Complex Gateway einschließlich Split-/Join-Semantik | ✅ Supported |
| BPMN-Ereignisse | Timer, Message, Signal, Error, Escalation, Cancel, Compensation und Terminate | ✅ Supported |
| BPMN-Scopes | Eingebettete und verschachtelte Subprozesse, Event Subprocesses, Call Activities und Transaktionen | ✅ Supported |
| BPMN Multi-Instance | Sequenzielle und parallele Multi-Instance-Ausführung, auch in wartenden Scopes | ✅ Supported |
| BPMN Timer | Catch- und Boundary-Timer mit ISO-8601 `timeDate`, `timeDuration` und `timeCycle` | ✅ Supported |
| BPMN Fehlerbehandlung | Interrupting/non-interrupting Boundary Events, hierarchische Propagation, Compensation in Rückwärtsreihenfolge und Transaction Cancel | ✅ Supported |
| BPMN Betrieb | Restart, mehrere API-Replikate, Idempotenz, Incident Recovery und Job Dead Letter | ✅ Supported |
| DMN Decision Tables | `UNIQUE`, `FIRST`, `PRIORITY`, `ANY`, `COLLECT`, `SUM`, `MIN`, `MAX`, `COUNT`, `RULE ORDER` und `OUTPUT ORDER` | ✅ Supported |
| DMN FEEL | Listen, Kontexte, Iterationen, Quantoren, temporale Typen, Built-ins, Unary Tests und fail-closed Syntaxprüfung | ✅ Supported |
| DMN DRD | Mehrstufige Decision Requirement Diagrams, Literal Expressions, Decision Services und Zyklusvalidierung | ✅ Supported |
| BPMN/DMN-Integration | Business Rule Tasks mit direktem und Zeebe-kompatiblem Binding | ✅ Supported |
| CMMN | Persistente Definitionen und Case-Lifecycle mit Plan Items, Case File, History, Sentries und verschachtelten Stages | ✅ Supported |
| CMMN Tasks und Events | Human, Manual und Service Tasks, User Events sowie Discretionary Items | ✅ Supported |

### Plattform und Produktfunktionen

| Bereich | Unterstützte Fähigkeiten | Status |
| --- | --- | --- |
| REST und OpenAPI | Tenantfähige Deployments, Runtime, Tasks, Decisions, Cases, Forms, Trigger, Migration, Analytics und Administration | ✅ Supported |
| gRPC und MCP | Maschinen- und Agenten-Schnittstellen für BPMN, DMN und CMMN | ✅ Supported |
| .NET SDK | Typisierte Client-APIs für Deployment, Start, Runtime, Trigger und Administration | ✅ Supported |
| CLI / .NET Tool | Validierung, Testlauf, Ausführung, Deployment, Registrierung, Status, Worker, Dashboard, Trigger, Credentials, Connectoren und Templates | ✅ Supported |
| Low-Code Studio | BPMN-/DMN-/CMMN-/Form-Modellierung, Import/Export, Properties, Quick Insert, Runtime-Overlay, Token-Simulation und Fehleranzeige | ✅ Supported |
| Persistenz | EF Core für Instanzen, Tokens, Variablen, Tasks, Jobs, Subscriptions, Incidents, Inbox, Outbox und Worker | ✅ Supported |
| Datenbanken | SQLite für lokale/Container-Profile sowie PostgreSQL und SQL Server für relationale Deployments | ✅ Supported |
| Messaging | RabbitMQ- und Kafka-Auslieferung aus der Outbox mit Retry, Dead Letter und Readiness | ✅ Supported |
| Workflow-Trigger | Persistente, tenantisolierte Webhook-Starts mit einmalig ausgegebenem Secret und serverseitigem Hash | ✅ Supported |
| Credentials und Connectoren | Secret-geschützte Credentials, Rotation, Connector-Verwaltung und wiederverwendbare Connector-Templates | ✅ Supported |
| Process Mining und Analytics | Transaktionale, idempotente Projektion, tenantfähige APIs, Prozessmetriken, Traces und Zeitreihen | ✅ Supported |
| Simulation | Deterministische Simulation mit hashgebundener Analytics-Auswertung | ✅ Supported |
| Live-Migration | Dry Run, Snapshot, atomare Migration, Rollback, Versionsmigration und Cross-Tenant-Schutz | ✅ Supported |
| Sicherheit | JWT/API-Key-Authentifizierung, Rollen und Policies, Mandantenisolation, Rate Limiting und fail-closed Produktionskonfiguration | ✅ Supported |
| Observability | Health, Liveness, Readiness, strukturierte Logs, OpenTelemetry und Prometheus-Metriken | ✅ Supported |
| Aspire AppHost | Orchestrierung von API, Studio, PostgreSQL und RabbitMQ mit Dependency- und Readiness-Modell | ✅ Supported |
| Release-Automatisierung | Ein Release-Build, reguläre Tests, tagversionierte SDK-/CLI-Pakete und Veröffentlichung über kurzlebige NuGet-OIDC-Berechtigungen | ✅ Supported |

### Weitere Produktfunktionen

- **Form Lifecycle:** tenantbezogene Form-Definitionen erstellen, lesen, aktualisieren, löschen, im Studio bearbeiten und zur Laufzeit anzeigen
- **AI Service Tasks:** Handler für OpenAI, Anthropic, Gemini, generische AI-Endpunkte, Context Enrichment und MCP-basierte Aufgaben
- **Plug-in-System:** validierte Plug-in-Assemblies, kontrollierte Aktivierung und fail-closed Produktionskonfiguration
- **External Worker Control Plane:** Worker-Registrierung, Heartbeats, Health, Pending Work, Rebalancing und Lastverteilung
- **Operations und Diagnose:** Instanzen suspendieren/fortsetzen, Incidents, Jobs, Variablen, History, Audit, Runtime Inspector, Performance- und Visual-Debug-Endpunkte
- **Identity und Tenancy:** Benutzer, Gruppen, Autorisierung, Tenant-Verwaltung sowie Camunda-orientierte Ressourcen für bestehende Integrationen
- **Webhook Ingress:** authentifizierter externer Eingang mit Korrelation in die persistente Runtime
- **Feature- und Dependency-Konfiguration:** API-gesteuerte Feature Flags und persistente Dependency Registry mit sicherer Konfigurationspriorität
- **n8n-Import:** Import von Workflows mit Credential-Referenzen, Bedingungs-Mapping und expliziten Review-Markierungen

## Architektur

```text
Studio / CLI / .NET SDK / REST / gRPC / MCP
                      |
                VertexBPMN.Api
                      |
     +----------------+----------------+
     |                |                |
 BPMN Runtime     DMN Engine       CMMN Runtime
     |                |                |
     +---------- Persistence ----------+
                EF Core / Outbox
                      |
       SQLite / PostgreSQL / SQL Server
                      |
             RabbitMQ / Kafka

VertexBPMN.AppHost orchestriert API, Studio, PostgreSQL,
RabbitMQ, Health Checks, Logs, Traces und Metriken.
```

Die Runtime speichert ihren Zustand dauerhaft. Jobs werden über Leases, Retry/Backoff und Dead Letter verarbeitet; Inbox/Outbox und idempotente Operationen schützen die Ausführung bei Neustarts und mehreren API-Replikaten.

## Schnellstart mit Aspire

Voraussetzungen:

- .NET 10 SDK
- Docker oder Podman für die normale Aspire-Containerorchestrierung; oder
- WSLC 2.9.3+ beziehungsweise lokal installierte PostgreSQL-/RabbitMQ-Dienste für den optionalen lokalen Fallback

Das Development-Profil startet API und Studio als .NET-Projekte und provisioniert PostgreSQL und RabbitMQ über Aspire:

```powershell
dotnet run --project src/VertexBPMN.AppHost --no-launch-profile -e DOTNET_ENVIRONMENT=Development
```

Das Containerprofil baut die API aus dem Root-Dockerfile, verwendet persistente SQLite-Dateien in einem Volume und startet das Studio lokal:

```powershell
dotnet run --project src/VertexBPMN.AppHost --no-launch-profile -e DOTNET_ENVIRONMENT=Containers
```

`Project` bleibt das Standardprofil und provisioniert PostgreSQL und RabbitMQ wie bisher über Aspire mit Docker oder Podman. `Containers` bleibt ebenfalls unverändert verfügbar. Nur wenn keine dieser Container-Runtimes lokal installiert ist, kann der opt-in Modus `ExternalServices` verwendet werden. API und Studio bleiben dabei im Aspire-AppHost, während die Infrastruktur außerhalb von DCP läuft.

Mit WSLC startet das Skript PostgreSQL und RabbitMQ automatisch:

```powershell
# Infrastruktur starten und AppHost im Vordergrund ausführen
dotnet restore VertexBPMN.sln
.\scripts\wslc-apphost.ps1

# Nur Infrastruktur verwalten
.\scripts\wslc-apphost.ps1 -InfrastructureOnly
.\scripts\wslc-apphost.ps1 -Action Status
.\scripts\wslc-apphost.ps1 -Action Stop
```

Bereits lokal laufende PostgreSQL- und RabbitMQ-Installationen werden nicht vom Skript verwaltet. Die fünf Datenbanken müssen dort vorhanden sein; Ports und lokale Zugangsdaten werden dem Launcher übergeben:

```powershell
.\scripts\wslc-apphost.ps1 -ExistingInfrastructure `
  -PostgresPort 5432 -RabbitMqPort 5672 `
  -User vertexbpmn -Password '<local-development-password>'
```

Das lokale Entwicklungskennwort kann vor dem Start über `VERTEXBPMN_WSLC_PASSWORD` gesetzt werden. Das Skript legt ein persistentes WSLC-Netzwerk, persistente Volumes und alle benötigten Datenbanken idempotent an. `Stop` entfernt weder Container noch Daten.
Standardmäßig veröffentlicht das WSLC-Profil PostgreSQL auf `55432`, RabbitMQ auf `55672` und dessen Management-Oberfläche auf `15673`; alle drei Ports sind Skriptparameter.
Beim ersten AppHost-Start benötigt Aspire Zugriff auf NuGet.org, um das zur AppHost-Version passende Aspire-CLI-Bundle aufzulösen.

| Dienst | Standardadresse |
| --- | --- |
| API | `http://localhost:51870` |
| Studio | `http://localhost:5263` |
| Swagger/OpenAPI | `http://localhost:51870/swagger` |
| Health | `http://localhost:51870/api/health` |
| Liveness | `http://localhost:51870/api/health/live` |
| Readiness | `http://localhost:51870/api/ready` |
| Prometheus | `http://localhost:51870/api/metrics/prometheus` |

Im normalen `Project`-Modus werden Zugangsdaten und Verbindungsinformationen weiterhin vom AppHost provisioniert. Im opt-in Modus `ExternalServices` werden sie nur für den gestarteten Prozess als Umgebungsvariablen gesetzt. Secrets gehören nicht in die AppHost-Konfiguration oder in das Repository.

## .NET SDK

Das öffentliche SDK kommuniziert mit einer laufenden VertexBPMN-API:

```bash
dotnet add package VertexBPMN.Sdk
```

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

var bpmnXml = await File.ReadAllTextAsync("order-process.bpmn");
var deployed = await client.DeployProcessAsync(
    bpmnXml,
    "order-process.bpmn",
    "acme");

var instance = await client.StartProcessAsync(
    deployed!.Key,
    new Dictionary<string, object?>
    {
        ["customerId"] = "C-42"
    },
    businessKey: "ORDER-123");

Console.WriteLine(instance?.Id);
```

## CLI und .NET Tool

Die CLI kann direkt aus dem Repository ausgeführt werden:

```powershell
dotnet run --project src/VertexBPMN.Cli -- --help
dotnet run --project src/VertexBPMN.Cli -- dashboard
```

Oder als .NET Tool installiert werden, sobald die gewünschte Version auf NuGet.org verfügbar ist:

```bash
dotnet tool install --global VertexBPMN.Cli
vertexbpmn --help
```

Wichtige Befehlsgruppen:

| Aufgabe | Befehle |
| --- | --- |
| Validierung und lokaler Test | `validate`, `test-run` |
| Prozessausführung | `execute`, `execute-id`, `execute-case` |
| Deployment und Registrierung | `deploy-bpmn`, `deploy-dmn`, `deploy-form`, `register-bpmn`, `register-cmmn`, `register-dmn` |
| Integrationen | `import-n8n`, `connector`, `template`, `credential` |
| Betrieb | `status`, `pending`, `workers` |
| Control Plane | `dashboard`, `studio`, `config` |
| Externe Starts | `trigger create`, `trigger list`, `trigger invoke`, `trigger enable`, `trigger disable`, `trigger delete` |

`dashboard` verwendet eine laufende API oder startet sie lokal, wartet auf Readiness, startet das Blazor Studio und öffnet das Dashboard. Der Alias `studio` führt denselben Workflow aus. Einstellungen können in [`src/VertexBPMN.Cli/appsettings.json`](src/VertexBPMN.Cli/appsettings.json) oder über `VERTEXBPMN_`-Umgebungsvariablen gesetzt werden.

## Low-Code Studio

`VertexBPMN.Studio` stellt eine browserbasierte Arbeitsoberfläche bereit:

- BPMN-, DMN-, CMMN- und Form-Modellierung mit gepinnten, reproduzierbaren bpmn.io-Assets
- Import, Export, Moddle-Erweiterungen und Properties-Panel
- Quick Insert und Low-Code-Mutationen
- Deployment von Definitionen und Verwaltung persistenter Workflow-Trigger
- Runtime-Viewer, Token-Overlay, Simulation und Fehlerdiagnose
- Prozessmigration mit Planung, Dry Run, Ausführung und Rollback
- Verwaltung von Credentials, Connectoren und Templates
- Chromium-basierte End-to-End-Tests gegen reale persistente API-Pfade

## Workflow-Trigger

Ein externer Workflow-Start besteht aus vier Schritten:

1. BPMN-Datei tenantbezogen deployen.
2. Trigger für den Process-Key registrieren.
3. Das einmalig ausgegebene Secret sicher speichern.
4. Trigger über REST, CLI, SDK oder Studio aufrufen.

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

Persistiert wird ausschließlich ein Hash des Secrets. Verwaltung und Aufrufe sind tenantisoliert. Details: [Workflow-Trigger Runbook](docs/runbooks/workflow-triggers.md).

## APIs und Integrationen

- **REST/OpenAPI:** Prozessdefinitionen und -instanzen, User Tasks, Decisions, Cases, Forms, Migration, Simulation, Analytics, Trigger, Credentials, Connectoren und Administration
- **gRPC:** typisierte Runtime- und Verwaltungsverträge
- **MCP:** Werkzeugzugriff für KI-Agenten auf BPMN-, DMN- und CMMN-Funktionen
- **RabbitMQ/Kafka:** zuverlässige externe Event-Auslieferung über die persistente Outbox
- **n8n Import:** Workflow-Import mit Credential-Referenzen, Mapping und Review-Markierungen
- **External Workers:** Registrierung, Heartbeats, Load-Balancing und Zustandsabfragen

Geschützte Endpunkte verwenden JWT oder API Keys sowie rollenbasierte Policies. Tenantkontext und Cross-Tenant-Regeln werden serverseitig validiert. Credentials geben über die API nur Metadaten und Secret-Key-Namen zurück; Klartext-Secrets werden weder in Responses noch in Audit-Details ausgegeben.

## Process Mining, Analytics und Observability

Runtime-Ereignisse werden persistent und transaktional projiziert. Die Plattform stellt unter anderem folgende Funktionen bereit:

- Prozess- und Event-Metriken, Event-Statistiken und Zeitreihen
- Ausführungstraces pro Prozessinstanz
- tenantbezogene Analytics-Abfragen
- Prognosen für Dauer, Abschluss und Bottlenecks
- Audit- und History-Abfragen
- Health-, Liveness- und Readiness-Probes
- OpenTelemetry für Logs, Traces und Metriken
- Prometheus-kompatible Laufzeit-, Job-, Incident-, Worker- und Outbox-Metriken

## Build, Tests und Qualifizierung

```powershell
dotnet restore VertexBPMN.sln
dotnet build VertexBPMN.sln --configuration Release --no-restore -p:SkipBpmnIoAssetBuild=true -m:1 --disable-build-servers
dotnet test tests/VertexBPMN.Tests/VertexBPMN.Tests.csproj --configuration Release --no-build --no-restore --filter-not-trait "Category=Phase3ExternalAcceptance" --max-parallel-test-modules 1
```

Der schnelle GitHub-Workflow führt genau die zentralen Blocker aus:

- Restore mit dem in `global.json` festgelegten .NET SDK
- einmaliger Release-Build unter Linux
- das zentrale Testprojekt einschließlich der persistenten Acceptance-Verträge; nur die externen RabbitMQ-/PostgreSQL-Tests werden ausgelassen
- Packen von SDK und CLI bei `v*`-Tags
- Veröffentlichung der gebauten Pakete über NuGet Trusted Publishing und OIDC

Dependency-Audits, Coverage, OpenAPI-/Conformance-Skripte, Container-Scans, SBOMs und echte Infrastrukturtests bleiben als lokale beziehungsweise gezielt ausführbare Prüfungen im Repository erhalten, blockieren aber nicht mehr jeden Build oder Release.

Weitere Befehle und Filter: [Build- und Test-Runbook](docs/runbooks/build-and-test.md).

## NuGet und Releases

- [`VertexBPMN.Sdk`](https://www.nuget.org/packages/VertexBPMN.Sdk) ist das öffentliche .NET-Clientpaket.
- [`VertexBPMN.Cli`](https://www.nuget.org/packages/VertexBPMN.Cli) ist als globales .NET Tool paketiert.

Normale CI-Läufe erzeugen beide Pakete als Artefakte, veröffentlichen sie aber nicht. Ein SemVer-Tag startet die qualifizierte Veröffentlichung. Nach erfolgreichen Gates tauscht GitHub OIDC über NuGet Trusted Publishing gegen kurzlebige Veröffentlichungsberechtigungen; ein dauerhafter NuGet API Key ist nicht erforderlich. Die tatsächliche Verfügbarkeit einer konkreten Version muss auf NuGet.org und in der zugehörigen GitHub-Release-Ausführung geprüft werden.

## Dokumentation

- [Dokumentationsübersicht](docs/README.md)
- [Produkt-Support- und Acceptance-Matrix](docs/reference/product-support-matrix.md)
- [Getting Started](docs/getting-started/README.md)
- [API Quickstart](docs/getting-started/api-quickstart.md)
- [Produktions-Deployment](docs/runbooks/production-deployment.md)
- [Security- und Release-Gates](docs/runbooks/security-and-release-gates.md)
- [Monitoring und Observability](docs/runbooks/monitoring.md)
- [Workflow-Trigger](docs/runbooks/workflow-triggers.md)
- [CLI im Wiki](https://github.com/VertexBPMN/VertexBPMN/wiki/CLI)
- [Aspire AppHost im Wiki](https://github.com/VertexBPMN/VertexBPMN/wiki/Aspire-AppHost)
- [Projektwebsite](https://vertexbpmn.com)

## Mitwirken

Beiträge sind willkommen. Bitte vor einem Pull Request:

1. ein Issue für größere Änderungen eröffnen,
2. Implementierung und Dokumentation gemeinsam aktualisieren,
3. relevante Unit-, Integrations- und Acceptance-Tests ergänzen,
4. Build und Tests lokal ausführen,
5. keine Secrets oder generierten Build-Artefakte committen.

## Lizenz

VertexBPMN ist unter der [Apache License 2.0](LICENSE) veröffentlicht.

## Danksagungen

Danke an die .NET-, bpmn.io- und Open-Source-Community sowie an alle Mitwirkenden, die VertexBPMN verbessern.

---

**VertexBPMN™** ist eine Marke von Tainosoft UG (haftungsbeschränkt).
