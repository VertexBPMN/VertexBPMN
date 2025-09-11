using System;
using System.Collections.Generic;

namespace VertexBPMN.Api.Dto
{
    public class SimulationResultDto
    {
    public string ProcessDefinitionId { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public List<SimulationStepDto> Steps { get; set; } = new();
    public bool Completed { get; set; }
    public string Message { get; set; } = string.Empty;
    }

    public class SimulationStepDto
    {
    public int StepNumber { get; set; }
    public string ActivityId { get; set; } = string.Empty;
    public string ActivityName { get; set; } = string.Empty;
    public Dictionary<string, object> Variables { get; set; } = new();
    public DateTime Timestamp { get; set; }
    }
}
