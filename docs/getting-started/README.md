# Getting Started

Dies ist der kanonische Einstieg fuer den lokalen Betrieb und den ersten API-Use-Case.
Wenn du nur schnell etwas testen willst, sind die Referenzen hier am Ende die naechsten sinnvollen Abzweige.

## Voraussetzungen

- .NET SDK 10
- Git
- Optional Docker

## Siehe auch

- [API Quickstart](api-quickstart.md)
- [C# Quickstart](csharp-quickstart.md)
- [OpenAPI Reference](../reference/openapi.md)
- [.NET SDK Reference](../reference/sdk-dotnet.md)

## API lokal starten

```powershell
dotnet restore VertexBPMN.sln
dotnet run --project src/VertexBPMN.Api/VertexBPMN.Api.csproj
```

API und Studio koennen gemeinsam gestartet werden:

```powershell
dotnet run --project src/VertexBPMN.Cli -- dashboard
```

Der CLI-Workflow verwendet standardmaessig:

| Dienst | Adresse |
| --- | --- |
| API | `http://localhost:51870` |
| Studio | `http://localhost:5263` |
| Health | `http://localhost:51870/api/Health` |
| Swagger | `http://localhost:51870/swagger` |

Pruefe den Health-Endpunkt:

```powershell
curl http://localhost:51870/api/Health
```

Die tatsaechliche URL kann durch Launch-Settings abweichen. Swagger und die CLI-Ausgabe sind massgeblich.

## Ersten Prozess deployen

Speichere dieses Modell als `hello-world.bpmn`:

```xml
<?xml version="1.0" encoding="UTF-8"?>
<bpmn:definitions xmlns:bpmn="http://www.omg.org/spec/BPMN/20100524/MODEL"
                  targetNamespace="https://vertexbpmn.dev/examples">
  <bpmn:process id="Process_HelloWorld" name="Hello World" isExecutable="true">
    <bpmn:startEvent id="StartEvent_1" />
    <bpmn:endEvent id="EndEvent_1" />
    <bpmn:sequenceFlow id="Flow_1" sourceRef="StartEvent_1" targetRef="EndEvent_1" />
  </bpmn:process>
</bpmn:definitions>
```

Deploye das Modell:

```powershell
$body = @{
  bpmnXml = Get-Content .\hello-world.bpmn -Raw
  name = "hello-world.bpmn"
  tenantId = $null
} | ConvertTo-Json

Invoke-RestMethod -Method Post `
  -Uri http://localhost:51870/api/repository `
  -ContentType application/json -Body $body
```

## Prozess starten

```powershell
$body = @{
  ProcessDefinitionKey = "Process_HelloWorld"
  Variables = @{ greeting = "Hello from VertexBPMN" }
  BusinessKey = "demo-001"
  TenantId = $null
} | ConvertTo-Json -Depth 10

Invoke-RestMethod -Method Post `
  -Uri http://localhost:51870/api/runtime/start `
  -ContentType application/json -Body $body
```
