using VertexBPMN.Domain.Model.Cmmn.Core;

namespace VertexBPMN.Domain.Model.Cmmn.PlanModel;

/// <summary>
/// Plan item (5.4.5, inherits from CMMNElement).
/// Extension: Added state for runtime.
/// </summary>
public record PlanItem(
    string Name,
    PlanItemDefinition DefinitionRef,
    List<EntryCriterion> EntryCriteria = null!,
    List<ExitCriterion> ExitCriteria = null!,
    PlanItemControl? ItemControl = null,
    PlanItemState State = PlanItemState.Available // Extension: Lifecycle state.
) : CMMNElement();