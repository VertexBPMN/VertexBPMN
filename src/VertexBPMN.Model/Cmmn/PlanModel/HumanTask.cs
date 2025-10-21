using VertexBPMN.Domain.Model.Cmmn.CaseModel;

namespace VertexBPMN.Domain.Model.Cmmn.PlanModel;

/// <summary>
/// Human task (5.4.9.1, inherits from Task).
/// </summary>
public record HumanTask(
    PlanningTable? PlanningTable = null,
    Role? PerformerRef = null
) : Task();