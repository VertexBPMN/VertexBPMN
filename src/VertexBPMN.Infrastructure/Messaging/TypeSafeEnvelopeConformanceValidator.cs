using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace VertexBPMN.Infrastructure.Messaging;

/// <summary>Ergebnis der semantischen Envelope-Konformitätsprüfung.</summary>
public enum EnvelopeConformanceVerdict
{
    /// <summary>Prüfung nicht aktiv / nicht anwendbar / extern nicht erreichbar (fail-open).</summary>
    NotEvaluated,
    /// <summary>Die Payload entspricht dem deklarierten Ereignisvertrag.</summary>
    Conforms,
    /// <summary>Pflichtfelder fehlen oder haben falsche Typen.</summary>
    Malformed,
    /// <summary>Felder vorhanden, aber semantisch ein anderes Ereignis.</summary>
    WrongContract,
    /// <summary>Urteil unsicher (unter Konfidenzschwelle) — zur erneuten Prüfung/Review.</summary>
    Unclear
}

/// <summary>Urteil + Konfidenz + optionaler Diagnosegrund.</summary>
public readonly record struct EnvelopeConformanceResult(
    EnvelopeConformanceVerdict Verdict,
    double Confidence = 0.0,
    string? Reason = null);

/// <summary>
/// Semantische Konformitäts-Judgment-Instanz auf Basis von TypeSafe System One (Jev).
/// Ergänzt die deterministischen Inbox-Checks (Envelope-Version, Idempotenz, Tenant): Sie bewertet
/// die tatsächliche Payload gegen den deklarierten Ereignisvertrag und liefert ein typisiertes Urteil.
/// </summary>
public interface ITypeSafeEnvelopeConformanceValidator
{
    Task<EnvelopeConformanceResult> EvaluateAsync(InboxEnvelope envelope, CancellationToken cancellationToken);
}

/// <summary>
/// Wandelt eine Envelope-Payload in einen TypeSafe-Choice-Judgment-Aufruf um und übersetzt das Choice-Urteil
/// in ein <see cref="EnvelopeConformanceVerdict"/>. Deaktiviert/nicht erreichbar → <see cref="EnvelopeConformanceVerdict.NotEvaluated"/>
/// (fail-open, damit ein externer Prüfdienst-Ausfall die Verarbeitung nicht blockiert).
/// Vertrags-Schemata kommen aus einer kleinen, aus dem P3-Inventar abgeleiteten Registry (Fallback: generisch).
/// </summary>
public sealed class TypeSafeEnvelopeConformanceValidator : ITypeSafeEnvelopeConformanceValidator
{
    private readonly ITypeSafeConformanceClient _client;
    private readonly TypeSafeConformanceOptions _options;
    private readonly ILogger<TypeSafeEnvelopeConformanceValidator> _logger;

    public TypeSafeEnvelopeConformanceValidator(
        ITypeSafeConformanceClient client,
        TypeSafeConformanceOptions options,
        ILogger<TypeSafeEnvelopeConformanceValidator> logger)
    {
        _client = client;
        _options = options;
        _logger = logger;
    }

    // Aus dem P3-Ereignisvertrags-Inventar abgeleitete minimale Vertrags-Schemata.
    private static readonly Dictionary<string, string> KnownContracts = new(StringComparer.Ordinal)
    {
        ["ServiceTaskDispatch"] = "Pflicht: targetWorkerId (string), implementation (string); optional: attributes (object), variables (object).",
        ["AiTaskDispatch"] = "Pflicht: targetWorkerId (string), aiProvider (string), aiModel (string); optional: attributes (object), variables (object).",
        ["ExecutionTokenPublished"] = "Trägt einen ExecutionToken (Prozesszustand, Metadaten, Variablen).",
        ["CaseTokenPublished"] = "Trägt einen CaseToken (CMMN-Zustände, Variablen).",
        ["TaskQueued"] = "Pflicht: taskId (string), taskType (string); optional: variables (object)."
    };

    private const string GenericContract = "Kein spezifischer Vertrag hinterlegt; bewerte, ob die Payload intern kohärent und wohlgeformt ist.";

    public async Task<EnvelopeConformanceResult> EvaluateAsync(InboxEnvelope envelope, CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
            return new(EnvelopeConformanceVerdict.NotEvaluated);

        if (string.IsNullOrWhiteSpace(envelope.EventType))
            return new(EnvelopeConformanceVerdict.Malformed, Reason: "Envelope hat keinen eventType.");

        KnownContracts.TryGetValue(envelope.EventType, out var contract);
        contract ??= GenericContract;

        var state = new
        {
            eventType = envelope.EventType,
            processInstanceId = envelope.ProcessInstanceId?.ToString() ?? string.Empty,
            tenantId = envelope.TenantId,
            declaredContract = contract,
            payload = envelope.Payload is { } p && p.ValueKind == JsonValueKind.Object
                ? p
                : (object?)envelope.Payload?.GetRawText() ?? string.Empty
        };

        var stateJson = JsonSerializer.Serialize(state);
        var answer = await _client.AskConformanceAsync(stateJson, cancellationToken);

        if (answer is null)
        {
            _logger.LogWarning("TypeSafe conformance unavailable for {Event}; fail-open (NotEvaluated).", envelope.EventType);
            return new(EnvelopeConformanceVerdict.NotEvaluated);
        }

        // Unzureichende Konfidenz => unsicher, nicht fälschlich als konform werten.
        if (answer.Confidence < _options.ConfidenceThreshold)
            return new(EnvelopeConformanceVerdict.Unclear, answer.Confidence,
                $"Konfidenz {answer.Confidence:0.00} unter Schwelle {_options.ConfidenceThreshold:0.00}.");

        return answer.Choice switch
        {
            "conforms" => new(EnvelopeConformanceVerdict.Conforms, answer.Confidence),
            "malformed" => new(EnvelopeConformanceVerdict.Malformed, answer.Confidence),
            "wrong_contract" => new(EnvelopeConformanceVerdict.WrongContract, answer.Confidence),
            _ => new(EnvelopeConformanceVerdict.Unclear, answer.Confidence, $"Unbekanntes Choice: {answer.Choice}")
        };
    }
}
