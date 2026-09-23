namespace VertexBPMN.Infrastructure.Messaging;

/// <summary>
/// Einstellungen für die optionale TypeSafe-Envelope-Konformitätsprüfung
/// (gebunden an <c>Runtime:TypeSafeConformance</c>). Standardmäßig deaktiviert:
/// Solange <see cref="Enabled"/> false ist, gibt der Validator <see cref="EnvelopeConformanceVerdict.NotEvaluated"/>
/// zurück und der Inbox-Pfad bleibt unverändert.
/// </summary>
public sealed class TypeSafeConformanceOptions
{
    /// <summary>Master-Schalter. false => der Validator wertet nicht aus (NotEvaluated).</summary>
    public bool Enabled { get; set; } = false;

    /// <summary>TypeSafe-System-One-Evaluations-Endpunkt.</summary>
    public string Endpoint { get; set; } = "https://api.typesafe.ai/v1/systemone";

    /// <summary>API-Key (server-seitig halten, niemals in Clients/Konfig-Repos).</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>TypeSafe-Modell (Alias bspw. "jev-latest").</summary>
    public string Model { get; set; } = "jev-latest";

    /// <summary>
    /// Minimale Choice-Konfidenz (max. Wahrscheinlichkeit), ab der ein Urteil als sicher gilt.
    /// Darunter wird das Ergebnis als <see cref="EnvelopeConformanceVerdict.Unclear"/> klassifiziert.
    /// </summary>
    public double ConfidenceThreshold { get; set; } = 0.6;
}
