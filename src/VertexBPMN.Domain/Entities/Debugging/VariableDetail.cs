namespace VertexBPMN.Domain.Entities.Debugging;

public class VariableDetail
{
    public string Name { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public bool IsExpandable { get; set; }
    public DateTime LastModified { get; set; }
}