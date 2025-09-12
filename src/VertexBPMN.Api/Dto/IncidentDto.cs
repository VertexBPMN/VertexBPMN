using System;

namespace VertexBPMN.Api.Dto;

public class IncidentDto
{
    public string Id { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public string ProcessDefinitionId { get; set; } = string.Empty;
    public string ProcessInstanceId { get; set; } = string.Empty;
    public string ExecutionId { get; set; } = string.Empty;
    public string IncidentType { get; set; } = string.Empty;
    public string ActivityId { get; set; } = string.Empty;
    public string CauseIncidentId { get; set; } = string.Empty;
    public string RootCauseIncidentId { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime? IncidentTimestamp { get; set; }
}
