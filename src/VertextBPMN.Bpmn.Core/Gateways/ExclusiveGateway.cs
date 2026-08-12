using VertexBPMN.Domain.Model.Bpmn.Common.Expressions;
using VertexBPMN.Domain.Model.Bpmn.Common.Flow;

namespace VertexBPMN.Domain.Model.Bpmn.Gateways;

public class ExclusiveGateway : Gateway
{
    public Expression? DefaultConditionExpression { get; set; }
}