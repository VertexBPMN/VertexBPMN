# VertexBPMN.Sdk

Typed .NET client for the VertexBPMN REST API.

```csharp
using VertexBPMN.Sdk;

var httpClient = new HttpClient
{
    BaseAddress = new Uri("https://localhost:5001")
};

var client = new VertexBpmnClient(httpClient, new VertexBpmnClientOptions
{
    BearerToken = "your-token",
    TenantId = "tenant-a"
});

var instance = await client.StartProcessAsync(
    "invoice-process",
    new Dictionary<string, object?> { ["invoiceId"] = "INV-42" });
```

The package contains no server, persistence, or credential storage dependencies. Configure the `HttpClient` lifetime and credentials in the consuming application.