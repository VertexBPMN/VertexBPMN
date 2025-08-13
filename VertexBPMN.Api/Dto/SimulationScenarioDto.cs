using System.Collections.Generic;

namespace VertexBPMN.Api.Dto
{
    public class SimulationScenarioDto
    {
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ProcessDefinitionId { get; set; } = string.Empty;
    public Dictionary<string, object> Variables { get; set; } = new();
    public int? MaxSteps { get; set; }
    public string TenantId { get; set; } = string.Empty;
    }
}
