namespace VertexBPMN.Domain.Model.Cmmn.PlanModel;

/// <summary>
/// Stage (5.4.8, inherits from PlanFragment).
/// Extension: Added state for runtime.
/// </summary>
public record Stage(
    bool AutoComplete = false,
    Expression? AutoCompleteCondition = null,
    PlanningTable? PlanningTable = null,
    PlanItemState State = PlanItemState.Available // Extension: Lifecycle state.
) : PlanFragment();