namespace VertexBPMN.Api.Migration;

internal class ProcessDefinitionInfo
{
    public Guid Id { get; set; }
    public string? TenantId { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Version { get; set; }
    public string BpmnXml { get; set; } = string.Empty;
}
