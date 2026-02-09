namespace VertexBPMN.Domain.Model.Bpmn.Data;

public class DataStoreReference : ItemAwareElement
{
    public DataStore? DataStoreRef { get; set; }
    public string? Name { get; set; }
}