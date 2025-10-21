using System.Collections.Generic;
using VertexBPMN.Domain.Model.Cmmn.Core;

namespace VertexBPMN.Domain.Model.Cmmn.InformationModel;

#nullable enable

/// <summary>
/// Case file as container (Figure 5.5, inherits from CMMNElement).
/// </summary>
public record CaseFile(
    List<CaseFileItem> CaseFileItems
) : CMMNElement();


/// <summary>
/// Case file item (Figure 5.5, inherits from CMMNElement).
/// Extension: Added state and version for runtime.
/// </summary>
public record CaseFileItem(
    string Name,
    MultiplicityEnum Multiplicity = MultiplicityEnum.ZeroOrOne,
    CaseFileItemDefinition DefinitionRef = null,
    List<CaseFileItem> Children = null!,
    CaseFileItem? Parent = null,
    List<CaseFileItem> TargetRefs = null!,
    CaseFileItem? SourceRef = null,
    CaseFileItemState State = CaseFileItemState.Available, // Extension: Lifecycle state.
    string? Version = null // Extension: Versioning.
) : CMMNElement();