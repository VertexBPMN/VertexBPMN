using System.Collections.Generic;
using VertexBPMN.Domain.Model.Bpmn.Common;
using VertexBPMN.Domain.Model.Bpmn.Foundation;

namespace VertexBPMN.Domain.Model.Bpmn.Data;

public record DataAssociation : BaseElement
{
    public Expression? Transformation { get; set; }
    public List<Assignment> Assignments { get; } = [];
    public List<ItemAwareElement> SourceRefs { get; } = [];
    public ItemAwareElement? TargetRef { get; set; }
}