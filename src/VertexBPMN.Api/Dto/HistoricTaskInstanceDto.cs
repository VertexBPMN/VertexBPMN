using System;

namespace VertexBPMN.Api.Dto;

public class HistoricTaskInstanceDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Assignee { get; set; } = string.Empty;
    public string TaskDefinitionKey { get; set; } = string.Empty;
    public string ProcessInstanceId { get; set; } = string.Empty;
    public string ProcessDefinitionId { get; set; } = string.Empty;
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public string DeleteReason { get; set; } = string.Empty;
}
