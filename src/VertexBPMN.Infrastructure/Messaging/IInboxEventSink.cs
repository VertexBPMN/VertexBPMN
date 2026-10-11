namespace VertexBPMN.Infrastructure.Messaging;

/// <summary>Alternative sink (test seams / external forwarding) invoked exactly once.</summary>
public interface IInboxEventSink
{
    Task HandleAsync(InboxEnvelope envelope, CancellationToken cancellationToken);
}
