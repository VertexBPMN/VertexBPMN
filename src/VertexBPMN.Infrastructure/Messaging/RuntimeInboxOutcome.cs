namespace VertexBPMN.Infrastructure.Messaging;

/// <summary>
/// Outcome classifications of one idempotent inbox processing attempt.
/// Consumers (RabbitMQ, Azure Service Bus) map these onto their ack/requeue/reject semantics.
/// </summary>
public enum RuntimeInboxOutcome
{
    /// <summary>Business effect was applied and the completion marker committed atomically.</summary>
    Completed,
    /// <summary>The message was already completed exactly once (idempotent no-op). Safe to acknowledge.</summary>
    CompletedDuplicate,
    /// <summary>The message is owned by another live worker and its claim has not expired. Safe to skip (ack).</summary>
    Busy,
    /// <summary>Transient failure; the consumer should redeliver/requeue and retry.</summary>
    RetryableFailure,
    /// <summary>Permanent, non-retryable failure (missing handler, invalid payload, unknown contract version,
    /// unauthorized tenant). The consumer should move the message to the DLQ, not ack as success.</summary>
    Rejected
}
