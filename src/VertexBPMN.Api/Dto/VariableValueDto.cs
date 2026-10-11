namespace VertexBPMN.Api.Dto;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515", Justification = "Public contract type VariableValueDto retained for exposed HTTP action signatures and controller activation (VariableValueDto).")]
public class VariableValueDto
{
    public string Type { get; set; } = string.Empty;
    public object? Value { get; set; }
    public IDictionary<string, object>? ValueInfo { get; set; }
}
