namespace VertexBPMN.Infrastructure.Messaging;

/// <summary>Outcome + an optional diagnostic reason (used for DLQ "dead-letter reason" and logs).</summary>
public readonly record struct RuntimeInboxProcessResult(RuntimeInboxOutcome Outcome, string? Reason = null);
