using VertexBPMN.Domain.Model.Bpmn.Common.Expressions;
using VertexBPMN.Domain.Model.Bpmn.Foundation;

namespace VertexBPMN.Domain.Model.Bpmn.Data;

public class Assignment : BaseElement
{
    public Expression? From { get; set; }
    public Expression? To { get; set; }
}