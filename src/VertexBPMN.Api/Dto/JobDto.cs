namespace VertexBPMN.Api.Dto;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515", Justification = "Public contract type JobDto retained for exposed HTTP action signatures and controller activation (JobDto).")]
public class JobDto
{
    public string Id { get; set; } = string.Empty;
    public string ProcessInstanceId { get; set; } = string.Empty;
    public string ExecutionId { get; set; } = string.Empty;
    public string ProcessDefinitionId { get; set; } = string.Empty;
    public string JobType { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public DateTime? DueDate { get; set; }
    public int Retries { get; set; }
    public string ExceptionMessage { get; set; } = string.Empty;
}
