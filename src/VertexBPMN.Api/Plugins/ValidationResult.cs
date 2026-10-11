namespace VertexBPMN.Api.Plugins;

internal class ValidationResult
{
    public bool IsValid { get; set; }
    public string Reason { get; set; } = string.Empty;
}
