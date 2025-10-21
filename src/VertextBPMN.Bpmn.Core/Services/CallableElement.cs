using VertexBPMN.Domain.Model.Bpmn.Foundation;

namespace VertexBPMN.Domain.Model.Bpmn.Services;

public abstract class CallableElement : RootElement
{
    public string? Name { get; set; }
}