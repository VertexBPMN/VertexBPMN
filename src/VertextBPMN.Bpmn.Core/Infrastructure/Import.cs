namespace VertexBPMN.Domain.Model.Bpmn.Infrastructure;

public class Import
{
    public required string ImportType { get; set; }
    public required string Location { get; set; }
    public required string Namespace { get; set; }
}