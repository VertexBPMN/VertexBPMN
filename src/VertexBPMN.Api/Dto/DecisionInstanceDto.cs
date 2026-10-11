namespace VertexBPMN.Api.Dto;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515", Justification = "Public contract type DecisionInstanceDto retained for exposed HTTP action signatures and controller activation (DecisionInstanceDto).")]
public class DecisionInstanceDto
{
    public string Id { get; set; } = string.Empty;
    public string DecisionKey { get; set; } = string.Empty;
    public object? Result { get; set; }
    public DateTime EvaluatedAt { get; set; }
    public string TenantId { get; set; } = string.Empty;
}
