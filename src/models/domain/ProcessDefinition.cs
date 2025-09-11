namespace VertexBPMN.Core.Domain;

/// <summary>
/// Represents a BPMN process definition deployed to the engine.
/// </summary>
public class ProcessDefinition
{
    public Guid Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Version { get; set; }
    public string BpmnXml { get; set; } = string.Empty;
    public string? TenantId { get; set; }
    public DateTime CreatedAt { get; set; }
    // TODO: Add additional metadata as needed
}
