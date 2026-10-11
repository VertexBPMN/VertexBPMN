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
