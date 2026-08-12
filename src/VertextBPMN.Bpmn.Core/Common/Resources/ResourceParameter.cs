using VertexBPMN.Domain.Model.Bpmn.Foundation;

namespace VertexBPMN.Domain.Model.Bpmn.Common.Resources;

public class ResourceParameter : BaseElement
{
    public string? Name { get; set; }
    public string? TypeRef { get; set; }
    public bool? IsRequired { get; set; }
}