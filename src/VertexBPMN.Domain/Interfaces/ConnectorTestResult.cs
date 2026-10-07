namespace VertexBPMN.Domain.Interfaces;
/// <summary>Redacted readiness result. It never contains credential material, request bodies or response bodies.</summary>
public sealed record ConnectorTestResult(bool Success, string Message, string? EndpointHost, bool CredentialConfigured);
