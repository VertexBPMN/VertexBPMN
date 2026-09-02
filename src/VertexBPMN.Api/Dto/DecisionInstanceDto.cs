namespace VertexBPMN.Api.Dto;

public class DecisionInstanceDto
{
    public string Id { get; set; } = string.Empty;
    public string DecisionKey { get; set; } = string.Empty;
    public object? Result { get; set; }
    public DateTime EvaluatedAt { get; set; }
    public string TenantId { get; set; } = string.Empty;
}