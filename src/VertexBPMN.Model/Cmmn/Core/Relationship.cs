namespace VertexBPMN.Domain.Model.Cmmn.Core;

/// <summary>
/// Relationship for extensibility (inherits from CMMNElement).
/// </summary>
public record Relationship(
    string Type,
    RelationshipDirection Direction = RelationshipDirection.None,
    List<CMMNElement> Sources = null!,
    List<CMMNElement> Targets = null!
) : CMMNElement();