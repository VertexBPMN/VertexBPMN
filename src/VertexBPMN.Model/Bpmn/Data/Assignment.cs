using VertexBPMN.Domain.Model.Bpmn.Common;
using VertexBPMN.Domain.Model.Bpmn.Foundation;

namespace VertexBPMN.Domain.Model.Bpmn.Data;

public record Assignment : BaseElement
{
    public Expression? From { get; set; }
    public Expression? To { get; set; }
}