using VertexBPMN.Domain.Model.Bpmn.Foundation;

namespace VertexBPMN.Domain.Model.Bpmn.Services;

public abstract class EndPoint : RootElement
{
    public string? Address { get; set; }
}