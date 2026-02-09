namespace VertexBPMN.Domain.Model.Security;

/// <summary>
/// Security validation result with detailed violation information.
/// </summary>
public sealed record ValidationResult
{
    public bool IsValid { get; set; } = true;
    public List<string> Violations { get; set; } = new();
}