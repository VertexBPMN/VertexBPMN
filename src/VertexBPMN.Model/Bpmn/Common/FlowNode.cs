using System.Collections.Generic;

namespace VertexBPMN.Domain.Model.Bpmn.Common;

#nullable enable

/// <summary>
/// Abstract flow node, as per Figure 8.22.
/// </summary>
public abstract record FlowNode(
    List<SequenceFlow> Incoming = null!,
    List<SequenceFlow> Outgoing = null!
) : FlowElement();