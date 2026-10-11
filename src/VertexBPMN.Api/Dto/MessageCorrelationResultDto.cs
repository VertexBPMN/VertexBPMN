namespace VertexBPMN.Api.Dto;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515", Justification = "Public contract type MessageCorrelationResultDto retained for exposed HTTP action signatures and controller activation (MessageCorrelationResultDto).")]
public class MessageCorrelationResultDto
{
    public string ResultType { get; set; } = string.Empty;
    public string ExecutionId { get; set; } = string.Empty;
    public string ProcessInstanceId { get; set; } = string.Empty;
    public string ProcessDefinitionId { get; set; } = string.Empty;
}
