using System.Collections.Generic;

namespace VertexBPMN.Api.Dto
{
    public class SimulationRequestDto
    {
    public string? BpmnXml { get; set; }
    public string ProcessDefinitionId { get; set; } = string.Empty;
    public Dictionary<string, object> Variables { get; set; } = new();
    public int? MaxSteps { get; set; }
    public string TenantId { get; set; } = string.Empty;
    }
}
