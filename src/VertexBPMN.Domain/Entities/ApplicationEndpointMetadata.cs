using System.Text.Json;

namespace VertexBPMN.Domain.Entities;

public static class ApplicationEndpointMetadata
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static IReadOnlyList<ApplicationEndpointDefinition> Read(string content)
    {
        using var document = JsonDocument.Parse(content);
        return document.RootElement.TryGetProperty("endpoints", out var endpoints)
            ? endpoints.Deserialize<ApplicationEndpointDefinition[]>(Options) ?? [] : [];
    }
}
