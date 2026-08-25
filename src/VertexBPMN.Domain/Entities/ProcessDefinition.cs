namespace VertexBPMN.Domain.Entities;

using System.Text.Json.Serialization;

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
    [JsonIgnore]
    public string TenantScope { get; set; } = "$global";
    public DateTime CreatedAt { get; set; }
    public Guid DeploymentId { get; set; }
    public EngineDeployment Deployment { get; set; } = null!;
    // TODO: Add additional metadata as needed
}
