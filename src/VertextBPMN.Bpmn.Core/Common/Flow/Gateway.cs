namespace VertexBPMN.Domain.Model.Bpmn.Common.Flow;

public abstract class Gateway : FlowNode
{
    public GatewayDirection GatewayDirection { get; set; } = GatewayDirection.Unspecified;
    public SequenceFlow? Default { get; set; }
}