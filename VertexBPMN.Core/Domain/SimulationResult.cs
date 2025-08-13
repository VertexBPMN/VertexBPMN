using System;
using System.Collections.Generic;

namespace VertexBPMN.Core.Domain
{
    public class SimulationResult
    {
        public string? BpmnXml { get; set; }
        public string ProcessDefinitionId { get; set; }
        public string TenantId { get; set; }
        public List<SimulationStep> Steps { get; set; } = new();
        public bool Completed { get; set; }
        public string Message { get; set; }
    }

    public class SimulationStep
    {
        public int StepNumber { get; set; }
        public string ActivityId { get; set; }
        public string ActivityName { get; set; }
        public Dictionary<string, object> Variables { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
