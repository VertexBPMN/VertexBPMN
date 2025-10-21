using VertexBPMN.Domain.Model.Bpmn.Enums;

namespace VertexBPMN.Domain.Model.Bpmn.Gateway;

#nullable enable

/// <summary>
/// Event based gateway, as per Figure 10.120.
/// </summary>
public record EventBasedGateway(
    bool Instantiate = false,
    EventBasedGatewayType EventGatewayType = EventBasedGatewayType.Exclusive
) : Common.Gateway;