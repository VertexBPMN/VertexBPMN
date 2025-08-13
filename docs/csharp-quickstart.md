# VertexBPMN C# Quickstart

Dieses Beispiel zeigt, wie du VertexBPMN über die REST-API aus einer C#-Anwendung ansprichst.

## Voraussetzungen
- .NET 9 SDK
- NuGet-Paket: `System.Net.Http.Json`

## Beispielcode
```csharp
using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

class Program
{
    static async Task Main()
    {
        var client = new HttpClient { BaseAddress = new Uri("http://localhost:5000/") };

        // 1. Prozessdefinition deployen
        var deployResponse = await client.PostAsJsonAsync("api/repository", new {
            bpmnXml = "<definitions ...>...</definitions>",
            name = "hello-world.bpmn"
        });
        deployResponse.EnsureSuccessStatusCode();
        var deployed = await deployResponse.Content.ReadFromJsonAsync<dynamic>();
        Console.WriteLine($"Deployed: {deployed}");

        // 2. Prozessinstanz starten
        var startResponse = await client.PostAsJsonAsync("api/runtime/start", new {
            ProcessDefinitionKey = "Process_HelloWorld",
            Variables = new { foo = 42 }
        });
        startResponse.EnsureSuccessStatusCode();
        var instance = await startResponse.Content.ReadFromJsonAsync<dynamic>();
        Console.WriteLine($"Started instance: {instance}");

        // 3. Tasks abfragen
        var tasks = await client.GetFromJsonAsync<dynamic>("api/task");
        Console.WriteLine($"Tasks: {tasks}");
    }
}
```

## Hinweise
- Die API ist OpenAPI/Swagger-dokumentiert.
- Für produktive Nutzung empfiehlt sich Authentifizierung und HTTPS.

---
*Letztes Update: August 2025*
