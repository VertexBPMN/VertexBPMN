namespace VertexBPMN.Domain.Model.Bpmn.Foundation;

public class ExtensionAttributeValue : BaseElement
{
    public required ExtensionAttributeDefinition Attribute { get; set; }
    public object? Value { get; set; }
}