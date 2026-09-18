namespace VertexBPMN.Infrastructure.Messaging;

/// <summary>
/// Consumer-side inbox settings (bound to <c>Runtime:Inbox</c>), kept separate from the outbox publisher
/// options as required by the Azure plan (P2). Time windows, lock renewal and delivery limits are configured
/// independently of outbox lease / max-attempts.
/// </summary>
public sealed class RuntimeInboxOptions
{
    /// <summary>Whether an inbox consumer should run. Defaults to the outbox enabled state when unset.</summary>
    public bool? Enabled { get; set; }

    /// <summary>Azure Service Bus topic subscription name (required when the entity is a Topic).</summary>
    public string Subscription { get; set; } = string.Empty;

    /// <summary>Maximum concurrent message callbacks per consumer (Service Bus Processor MaxConcurrentCalls).</summary>
    public int MaxConcurrentCalls { get; set; } = 8;

    /// <summary>Broker prefetch count.</summary>
    public int PrefetchCount { get; set; } = 16;

    /// <summary>Azure Service Bus MaxDeliveryCount: redeliveries before a message is dead-lettered.</summary>
    public int MaxDeliveryCount { get; set; } = 10;

    /// <summary>Seconds of lock renewal for long-running handlers (0 = no renewal).</summary>
    public int LockRenewalSeconds { get; set; } = 0;

    /// <summary>
    /// Seconds after which an incomplete inbox claim is considered stale and reclaimable (recovery after a
    /// crash mid-processing). Separately configured from the outbox lease.
    /// </summary>
    public int ClaimTimeoutSeconds { get; set; } = 120;
}
