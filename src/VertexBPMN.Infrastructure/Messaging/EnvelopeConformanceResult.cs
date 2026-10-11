namespace VertexBPMN.Infrastructure.Messaging;

/// <summary>Urteil + Konfidenz + optionaler Diagnosegrund.</summary>
public readonly record struct EnvelopeConformanceResult(
    EnvelopeConformanceVerdict Verdict,
    double Confidence = 0.0,
    string? Reason = null);
