using VertexBPMN.Domain.Model.Bpmn.Common;

namespace VertexBPMN.Domain.Model.Bpmn.Gateway;

#nullable enable

/// <summary>
/// Complex gateway, as per Figure 10.114.
/// </summary>
public record ComplexGateway(
    Expression? ActivationCondition = null,
    SequenceFlow? Default = null
) : Common.Gateway;