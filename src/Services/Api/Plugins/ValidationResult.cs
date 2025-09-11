namespace VertexBPMN.Api.Plugins;

public class ValidationResult
{
    public bool IsValid { get; set; }
    public string Reason { get; set; } = string.Empty;
}