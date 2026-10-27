using System.Text.Json;
using VertexBPMN.Domain.Entities;

namespace VertexBPMN.Api.Services;

internal static class EndpointPathParameters
{
    internal static JsonElement? Read(ApplicationEndpointDefinition endpoint, string? path)
    {
        var template = endpoint.Route.Split('/').Skip(3).ToArray();
        var actual = string.IsNullOrEmpty(path) ? [] : path.Split('/');
        if (template.Length != actual.Length) { return null; }
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var index = 0; index < template.Length; index++)
        {
            var segment = template[index];
            if (segment.StartsWith('{')) { values.Add(segment[1..^1], actual[index]); }
            else if (segment != actual[index]) { return null; }
        }
        return JsonSerializer.SerializeToElement(values);
    }
}
