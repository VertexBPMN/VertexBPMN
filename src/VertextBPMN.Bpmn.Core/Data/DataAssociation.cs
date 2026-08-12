using System.Collections.Generic;
using VertexBPMN.Domain.Model.Bpmn.Common.Expressions;
using VertexBPMN.Domain.Model.Bpmn.Foundation;

namespace VertexBPMN.Domain.Model.Bpmn.Data;

public class DataAssociation : BaseElement
{
    public Expression? Transformation { get; set; }
    public IReadOnlyList<Assignment> Assignments { get; } = [];
    public IReadOnlyList<ItemAwareElement> SourceRefs { get; } = [];
    public ItemAwareElement? TargetRef { get; set; }
}