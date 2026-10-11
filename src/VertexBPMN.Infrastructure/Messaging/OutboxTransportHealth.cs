namespace VertexBPMN.Infrastructure.Messaging;

public sealed record OutboxTransportHealth(bool IsHealthy, string Description);
