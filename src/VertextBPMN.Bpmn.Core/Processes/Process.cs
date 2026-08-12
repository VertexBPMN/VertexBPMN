using System.Collections.Generic;
using VertexBPMN.Domain.Model.Bpmn.Common.Flow;
using VertexBPMN.Domain.Model.Bpmn.Data;

namespace VertexBPMN.Domain.Model.Bpmn.Processes;

public class Process : FlowElementsContainer
{
    public string? Name { get; set; }
    public bool? IsExecutable { get; set; }
    public bool? IsClosed { get; set; }
    public string? ProcessType { get; set; }
    public IReadOnlyList<LaneSet> LaneSets { get; } = [];
    public IReadOnlyList<Property> Properties { get; } = [];
    public IOSpecification? IoSpecification { get; set; }
}