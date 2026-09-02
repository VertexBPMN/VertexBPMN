namespace VertexBPMN.Api.Migration;

public class TokenState
{
    public Guid Id { get; set; }
    public Guid ProcessInstanceId { get; set; }
    public string ActivityId { get; set; } = string.Empty;
    public string NodeType { get; set; } = string.Empty;
    public string? State { get; set; }
    public Dictionary<string, object> Variables { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public string? AssignedWorker { get; set; }
    public DateTime? AssignedAt { get; set; }
    public int RetryCount { get; set; }
}
