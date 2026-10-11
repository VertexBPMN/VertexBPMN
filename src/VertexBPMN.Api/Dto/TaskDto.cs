namespace VertexBPMN.Api.Dto;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515", Justification = "Public contract type TaskDto retained for exposed HTTP action signatures and controller activation (TaskDto).")]
public class TaskDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Assignee { get; set; } = string.Empty;
    public string Owner { get; set; } = string.Empty;
    public string TaskDefinitionKey { get; set; } = string.Empty;
    public string ProcessInstanceId { get; set; } = string.Empty;
    public string ProcessDefinitionId { get; set; } = string.Empty;
    public string ExecutionId { get; set; } = string.Empty;
    public int Priority { get; set; }
    public DateTime? Due { get; set; }
    public DateTime? FollowUp { get; set; }
    public DateTime? Created { get; set; }
    public IDictionary<string, object>? Variables { get; set; }
        public string? FormKey { get; set; }
        public string? FormSchema { get; set; }
}
