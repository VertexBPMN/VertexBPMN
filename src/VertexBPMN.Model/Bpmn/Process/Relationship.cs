using System.Collections.Generic;
using VertexBPMN.Domain.Model.Bpmn.Enums;
using VertexBPMN.Domain.Model.Bpmn.Foundation;

namespace VertexBPMN.Domain.Model.Bpmn.Process;

#nullable enable

/// <summary>
/// Relationship, as per the specification.
/// </summary>
public record Relationship(
    string Type,
    RelationshipDirection Direction = RelationshipDirection.None,
    List<BaseElement> Sources = null!,
    List<BaseElement> Targets = null!,
    string? Id = null) : BaseElement;