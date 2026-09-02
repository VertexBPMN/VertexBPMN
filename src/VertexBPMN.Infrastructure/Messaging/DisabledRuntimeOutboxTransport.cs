using VertexBPMN.Domain.Entities;

namespace VertexBPMN.Infrastructure.Messaging;

public sealed class DisabledRuntimeOutboxTransport : IRuntimeOutboxTransport
{
    public ValueTask PublishAsync(RuntimeOutboxMessage message, CancellationToken cancellationToken = default) =>
        ValueTask.FromException(new InvalidOperationException("The runtime outbox transport is disabled."));

    public ValueTask<OutboxTransportHealth> CheckHealthAsync(CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(new OutboxTransportHealth(false, "Runtime outbox transport is disabled."));
}
