using System.Collections.Generic;
using VertexBPMN.Domain.Model.Bpmn.Foundation;

namespace VertexBPMN.Domain.Model.Bpmn.Processes;

public class LaneSet : BaseElement
{
    public string? Name { get; set; }
    public IReadOnlyList<Lane> Lanes { get; } = [];
}