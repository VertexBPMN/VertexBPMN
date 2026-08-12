namespace VertexBPMN.Domain.Entities
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
}
