namespace VertexBPMN.Domain.Model.Security;

/// <summary>
/// Result of content security validation.
/// </summary>
public sealed record ContentValidationResult
{
    public bool IsSecure { get; set; } = true;
    public List<SecurityThreat> Threats { get; set; } = new();
}