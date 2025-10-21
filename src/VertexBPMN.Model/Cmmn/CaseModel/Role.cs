using VertexBPMN.Domain.Model.Cmmn.Core;

namespace VertexBPMN.Domain.Model.Cmmn.CaseModel;

/// <summary>
/// Role for authorization (5.2.2, inherits from CMMNElement).
/// </summary>
public record Role(
    string Name,
    Case Case
) : CMMNElement();