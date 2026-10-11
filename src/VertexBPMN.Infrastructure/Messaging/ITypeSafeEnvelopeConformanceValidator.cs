namespace VertexBPMN.Infrastructure.Messaging;

/// <summary>
/// Semantische Konformitäts-Judgment-Instanz auf Basis von TypeSafe System One (Jev).
/// Ergänzt die deterministischen Inbox-Checks (Envelope-Version, Idempotenz, Tenant): Sie bewertet
/// die tatsächliche Payload gegen den deklarierten Ereignisvertrag und liefert ein typisiertes Urteil.
/// </summary>
public interface ITypeSafeEnvelopeConformanceValidator
{
    Task<EnvelopeConformanceResult> EvaluateAsync(InboxEnvelope envelope, CancellationToken cancellationToken);
}
