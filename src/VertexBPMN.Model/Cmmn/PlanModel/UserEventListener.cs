using VertexBPMN.Domain.Model.Cmmn.CaseModel;

namespace VertexBPMN.Domain.Model.Cmmn.PlanModel;

/// <summary>
/// User event listener (Figure 5.7, inherits from EventListener).
/// </summary>
public record UserEventListener(
    List<Role> AuthorizedRoleRefs = null!
) : EventListener();