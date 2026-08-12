using VertexBPMN.Domain.Model.Bpmn.Common.Flow;

namespace VertexBPMN.Domain.Model.Bpmn.Gateways;

public class EventBasedGateway : Gateway
{
    public bool? Instantiate { get; set; }
}