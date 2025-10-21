namespace VertexBPMN.Domain.Model.Cmmn.Core;

/// <summary>
/// Property for case file item definition (5.1.4.1, inherits from CMMNElement).
/// </summary>
public record Property(
    string Name,
    PropertyType Type = PropertyType.Unspecified
) : CMMNElement();