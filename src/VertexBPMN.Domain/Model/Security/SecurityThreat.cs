namespace VertexBPMN.Domain.Model.Security;

/// <summary>
/// Detected security threat information.
/// </summary>
public sealed record SecurityThreat
{
    public ThreatType Type { get; init; }
    public string? Pattern { get; init; }
    public int Occurrences { get; init; } = 1;
    public string? FirstMatch { get; init; }
    public string? Description { get; init; }
}