namespace VertexBPMN.Domain.Entities
{
    public class SimulationResult
    {
        public string? BpmnXml { get; set; }
        public string DefinitionHash { get; set; } = string.Empty;
        public string ProcessDefinitionId { get; set; } = string.Empty;
        public string TenantId { get; set; } = string.Empty;
        public List<SimulationStep> Steps { get; set; } = new();
        public bool Completed { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
