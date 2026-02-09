namespace VertexBPMN.Domain.Model.Security;

/// <summary>
/// Enhanced security validation result with comprehensive information.
/// </summary>
public sealed record SecurityValidationResult
{
    public bool IsSecure { get; set; } = true;
    public bool DtdProcessingDisabled { get; set; }
    public bool ExternalEntityResolutionDisabled { get; set; }
    public List<string> Vulnerabilities { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public string? SecurityHash { get; set; }
    public DateTimeOffset ValidationTimestamp { get; set; }
}