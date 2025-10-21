namespace VertexBPMN.Domain.Model.Dmn.DecisionRequirement;

/// <summary>
/// PerformanceIndicator (extends BusinessContextElement).
/// </summary>
public record PerformanceIndicator(
    List<Decision> ImpactingDecisions = null!
) : BusinessContextElement();