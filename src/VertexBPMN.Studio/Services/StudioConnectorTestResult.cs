namespace VertexBPMN.Studio.Services;
internal sealed record StudioConnectorTestResult(bool Success, string Message, string? EndpointHost, bool CredentialConfigured);
