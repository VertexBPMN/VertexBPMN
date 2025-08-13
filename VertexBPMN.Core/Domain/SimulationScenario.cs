using System.Collections.Generic;

namespace VertexBPMN.Core.Domain
{
    public class SimulationScenario
    {
        public string? BpmnXml { get; set; }
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string ProcessDefinitionId { get; set; }
        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public Dictionary<string, object> Variables { get; set; }
        public int? MaxSteps { get; set; }
        public string TenantId { get; set; }
    }
}
