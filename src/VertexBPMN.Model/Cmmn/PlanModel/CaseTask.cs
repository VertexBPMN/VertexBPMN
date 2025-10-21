using VertexBPMN.Domain.Model.Cmmn.CaseModel;

namespace VertexBPMN.Domain.Model.Cmmn.PlanModel;

/// <summary>
/// Case task (5.4.9.3, inherits from Task).
/// </summary>
public record CaseTask(
    Case CaseRef
) : Task();