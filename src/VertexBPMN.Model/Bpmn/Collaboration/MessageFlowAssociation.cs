using VertexBPMN.Domain.Model.Bpmn.Foundation;

namespace VertexBPMN.Domain.Model.Bpmn.Collaboration;

#nullable enable

/// <summary>
/// Message flow association, as per Figure 9.15.
/// </summary>
public record MessageFlowAssociation(
    MessageFlow InnerMessageFlowRef,
    MessageFlow OuterMessageFlowRef
) : BaseElement;