using VertexBPMN.Domain.Model.Bpmn.Common;
using VertexBPMN.Domain.Model.Bpmn.Foundation;

namespace VertexBPMN.Domain.Model.Bpmn.Collaboration;

#nullable enable

/// <summary>
/// Message flow, as per Figure 9.14.
/// </summary>
public record MessageFlow(
    string? Name,
    InteractionNode SourceRef,
    InteractionNode TargetRef,
    Message? MessageRef = null
) : BaseElement;