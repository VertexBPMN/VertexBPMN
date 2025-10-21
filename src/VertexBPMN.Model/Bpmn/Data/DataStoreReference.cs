namespace VertexBPMN.Domain.Model.Bpmn.Data;

public record DataStoreReference : ItemAwareElement
{
    public DataStore? DataStoreRef { get; set; }
    public string? Name { get; set; }
}