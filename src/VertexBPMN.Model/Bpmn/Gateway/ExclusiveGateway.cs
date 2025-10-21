using VertexBPMN.Domain.Model.Bpmn.Common;

namespace VertexBPMN.Domain.Model.Bpmn.Gateway;

#nullable enable

/// <summary>
/// Exclusive gateway, as per Figure 10.107.
/// </summary>
public record ExclusiveGateway(
    SequenceFlow? Default = null
) : Common.Gateway;