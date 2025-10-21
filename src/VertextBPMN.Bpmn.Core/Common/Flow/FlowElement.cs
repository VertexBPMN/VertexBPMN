using System.Collections.Generic;
using VertexBPMN.Domain.Model.Bpmn.Common.Artifacts;
using VertexBPMN.Domain.Model.Bpmn.Foundation;

namespace VertexBPMN.Domain.Model.Bpmn.Common.Flow;

public abstract class FlowElement : BaseElement
{
    public string? Name { get; set; }
    public Auditing? Auditing { get; set; }
    public Monitoring? Monitoring { get; set; }
    public IReadOnlyList<CategoryValue> CategoryValueRefs { get; } = [];
}