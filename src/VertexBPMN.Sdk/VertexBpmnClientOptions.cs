namespace VertexBPMN.Sdk;

public sealed class VertexBpmnClientOptions
{
    public VertexBpmnEngineType? ExpectedEngineType { get; init; }

    public string? BearerToken { get; init; }

    public string? ApiKey { get; init; }

    public string? TenantId { get; init; }
}