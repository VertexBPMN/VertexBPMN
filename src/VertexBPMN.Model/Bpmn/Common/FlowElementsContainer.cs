using System.Collections.Generic;
using VertexBPMN.Domain.Model.Bpmn.Foundation;
using VertexBPMN.Domain.Model.Bpmn.Process;

namespace VertexBPMN.Domain.Model.Bpmn.Common;

#nullable enable

/// <summary>
/// Flow elements container, as per Figure 8.23.
/// </summary>
public abstract record FlowElementsContainer(
    List<LaneSet> LaneSets = null!,
    List<FlowElement> FlowElements = null!
) : BaseElement();