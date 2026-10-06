namespace VertexBPMN.Domain.Interfaces;

public sealed record ConnectorWriteRequest(string Name, string Type, string? Description, string? Endpoint, string? CredentialId, string? TemplateId = null, bool Enabled = true);
