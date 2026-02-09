using System.Collections.Generic;
using VertexBPMN.Domain.Model.Bpmn.Common;

namespace VertexBPMN.Domain.Model.Bpmn.Foundation;

public class Relationship : BaseElement
{
    public required string Type { get; set; }
    public RelationshipDirection Direction { get; set; } = RelationshipDirection.None;
    public IReadOnlyList<BaseElement> Sources { get; } = [];
    public IReadOnlyList<BaseElement> Targets { get; } = [];
}