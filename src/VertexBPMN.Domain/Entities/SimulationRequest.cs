namespace VertexBPMN.Domain.Entities
{
    public class SimulationRequest
    {
        public string BpmnXml { get; set; } = string.Empty;
        public string ProcessDefinitionId { get; set; } = string.Empty;
        public Dictionary<string, object> Variables { get; set; } = new();
        public int? MaxSteps { get; set; }
        public string TenantId { get; set; } = string.Empty;
        public Dictionary<string, string> EventSelections { get; set; } = new();
    }
}
