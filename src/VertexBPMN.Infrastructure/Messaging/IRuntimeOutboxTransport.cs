using VertexBPMN.Domain.Entities;

namespace VertexBPMN.Infrastructure.Messaging;

public interface IRuntimeOutboxTransport
{
    ValueTask PublishAsync(RuntimeOutboxMessage message, CancellationToken cancellationToken = default);
    ValueTask<OutboxTransportHealth> CheckHealthAsync(CancellationToken cancellationToken = default);
}

public sealed record OutboxTransportHealth(bool IsHealthy, string Description);
