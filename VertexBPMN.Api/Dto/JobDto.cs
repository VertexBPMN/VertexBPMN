using System;

namespace VertexBPMN.Api.Dto;

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
