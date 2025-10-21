using VertexBPMN.Domain.Model.Bpmn.Common;

namespace VertexBPMN.Domain.Model.Bpmn.Gateway;

#nullable enable

/// <summary>
/// Inclusive gateway, as per Figure 10.109.
/// </summary>
public record InclusiveGateway(
    SequenceFlow? Default = null
) : Common.Gateway;