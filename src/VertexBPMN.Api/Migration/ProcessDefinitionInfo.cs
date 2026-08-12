namespace VertexBPMN.Api.Migration;

public class ProcessDefinitionInfo
{
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Version { get; set; }
    public string BpmnXml { get; set; } = string.Empty;
}