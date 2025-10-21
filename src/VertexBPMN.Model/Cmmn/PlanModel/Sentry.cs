using VertexBPMN.Domain.Model.Cmmn.Core;

namespace VertexBPMN.Domain.Model.Cmmn.PlanModel;

/// <summary>
/// Sentry (Figure 5.9, inherits from CMMNElement).
/// Extension: Added isSatisfied flag for runtime.
/// </summary>
public record Sentry(
    string Name,
    List<OnPart> OnParts = null!,
    IfPart? IfPart = null,
    bool IsSatisfied = false // Extension: Runtime evaluation result.
) : CMMNElement();