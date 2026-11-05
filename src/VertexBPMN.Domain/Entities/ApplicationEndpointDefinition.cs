using System.Text.Json;

namespace VertexBPMN.Domain.Entities;

/// <summary>Authoring metadata only; this contract does not expose an executable route.</summary>
public sealed record ApplicationEndpointDefinition(
    string Key, string Method, string Route, IReadOnlyList<string> PathParameters,
    JsonElement InputSchema, JsonElement OutputSchema, string Permission,
    ApplicationEndpointLimits Limits, ApplicationEndpointActionBinding Action);
