using VertexBPMN.Domain.Model.Cmmn.Core;

namespace VertexBPMN.Domain.Model.Cmmn.PlanModel;

/// <summary>
/// Planning table for discretionary items (5.4.9.1.1, inherits from CMMNElement).
/// </summary>
public record PlanningTable(
    List<DiscretionaryItem> TableItems = null!,
    List<ApplicabilityRule> ApplicabilityRules = null!
) : CMMNElement();