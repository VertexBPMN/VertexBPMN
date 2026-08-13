# .NET SDK

## Installation

```powershell
dotnet add package VertexBPMN.Sdk
```

Das Paket targetiert aktuell `net10.0` und enthält einen typisierten REST-Client. Es startet keine Engine und speichert keine Zugangsdaten.

## Client verwenden

```csharp
using VertexBPMN.Sdk;

var httpClient = new HttpClient
{
    BaseAddress = new Uri("https://bpmn.example.com")
};

var client = new VertexBpmnClient(httpClient, new VertexBpmnClientOptions
{
    BearerToken = Environment.GetEnvironmentVariable("VERTEXBPMN_TOKEN"),
    ApiKey = Environment.GetEnvironmentVariable("VERTEXBPMN_API_KEY"),
    TenantId = "sales"
});

var definitions = await client.ListProcessDefinitionsAsync(key: "OrderProcess");
var instance = await client.StartProcessAsync(
    "OrderProcess",
    new Dictionary<string, object?>
    {
        ["orderId"] = "ORD-1007",
        ["amount"] = 125.50m
    },
    businessKey: "ORD-1007");

if (instance is not null)
    Console.WriteLine($"Started {instance.Id}");
```

## Tasks bearbeiten

```csharp
var tasks = await client.ListTasksAsync(assignee: "alice");
var task = tasks.FirstOrDefault();

if (task is not null)
{
    await client.ClaimTaskAsync(task.Id, "alice");
    await client.CompleteTaskAsync(task.Id, new Dictionary<string, object?>
    {
        ["approved"] = true
    });
}
```

## HttpClient-Lebensdauer

Verwende in ASP.NET Core `IHttpClientFactory` oder einen langlebigen `HttpClient`:

```csharp
builder.Services.AddHttpClient("vertexbpmn", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["VertexBpmn:BaseUrl"]!);
});
```

Tokens sollten kurzlebig sein und nicht in `appsettings.json` committed werden.
