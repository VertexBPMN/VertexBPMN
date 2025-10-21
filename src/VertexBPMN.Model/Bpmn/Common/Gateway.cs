using VertexBPMN.Domain.Model.Bpmn.Enums;

namespace VertexBPMN.Domain.Model.Bpmn.Common;

#nullable enable

/// <summary>
/// Abstract gateway, as per Figure 8.24.
/// Represents a BPMN gateway controlling divergence/convergence of sequence flows.
/// </summary>
/// <param name="GatewayDirection">Direction classification of the gateway.</param>
public abstract record Gateway(
    GatewayDirection GatewayDirection = GatewayDirection.Unspecified
) : FlowNode()
{
    /// <summary>
    /// BPMN gateway type identifier (e.g., "exclusive", "inclusive", "parallel", "complex", "eventBased").
    /// </summary>
    public string? Type { get; init; }

    /// <summary>
    /// Optional default outgoing sequence flow.
    /// For Exclusive / Inclusive gateways this flow is chosen when no other condition evaluates to true.
    /// Must be one of the elements in <see cref="Outgoing"/>.
    /// </summary>
    public SequenceFlow? Default { get; init; }
}