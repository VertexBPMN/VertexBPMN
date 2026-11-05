namespace VertexBPMN.Domain.Entities;

public sealed record ApplicationEndpointLimits(int TimeoutSeconds, int MaxRequestBytes, int MaxResponseBytes);
