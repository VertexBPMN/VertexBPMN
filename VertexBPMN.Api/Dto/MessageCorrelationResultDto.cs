using System;

namespace VertexBPMN.Api.Dto;

public class MessageCorrelationResultDto
{
    public string ResultType { get; set; } = string.Empty;
    public string ExecutionId { get; set; } = string.Empty;
    public string ProcessInstanceId { get; set; } = string.Empty;
    public string ProcessDefinitionId { get; set; } = string.Empty;
}
