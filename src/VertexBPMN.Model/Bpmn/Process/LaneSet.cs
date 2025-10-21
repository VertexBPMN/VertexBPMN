using System.Collections.Generic;
using VertexBPMN.Domain.Model.Bpmn.Foundation;

namespace VertexBPMN.Domain.Model.Bpmn.Process;

#nullable enable

/// <summary>
/// Lane set, as per Figure 10.126.
/// </summary>
public record LaneSet(
    string? Name = null,
    List<Lane> Lanes = null!
) : BaseElement;