namespace VertexBPMN.Domain.Model.Bpmn.Foundation;

public class ExtensionAttributeDefinition : BaseElement
{
    public required string Name { get; set; }
    public string? TypeRef { get; set; }
    public bool IsReference { get; set; }
}