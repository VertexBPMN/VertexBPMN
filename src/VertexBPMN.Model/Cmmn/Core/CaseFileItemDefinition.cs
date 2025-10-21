namespace VertexBPMN.Domain.Model.Cmmn.Core;

/// <summary>
/// Case file item definition (5.1.4, inherits from CMMNElement).
/// Extension: Added versioning attributes for runtime.
/// </summary>
public record CaseFileItemDefinition(
    string? Name = null,
    DefinitionType DefinitionType = DefinitionType.Unspecified,
    string? StructureRef = null,
    Import? ImportRef = null,
    List<Property> Properties = null!,
    string? Version = null // Extension: Versioning (out-of-scope in spec, but useful).
) : CMMNElement();