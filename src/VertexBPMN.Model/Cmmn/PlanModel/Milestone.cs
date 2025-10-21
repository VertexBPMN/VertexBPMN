namespace VertexBPMN.Domain.Model.Cmmn.PlanModel;

/// <summary>
/// Milestone (Figure 5.8, inherits from PlanItemDefinition).
/// Extension: Added state for runtime.
/// </summary>
public record Milestone(
    EventMilestoneState State = EventMilestoneState.Available // Extension: Lifecycle state.
) : PlanItemDefinition(Name: string.Empty);