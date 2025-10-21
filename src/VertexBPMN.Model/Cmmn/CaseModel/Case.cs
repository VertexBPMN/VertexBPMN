using System.Collections.Generic;
using VertexBPMN.Domain.Model.Cmmn.Core;
using VertexBPMN.Domain.Model.Cmmn.InformationModel;
using VertexBPMN.Domain.Model.Cmmn.PlanModel;

namespace VertexBPMN.Domain.Model.Cmmn.CaseModel;

#nullable enable

/// <summary>
/// Case as top-level element (Figure 5.4, inherits from CMMNElement).
/// Extension: Added runtime state.
/// </summary>
public record Case(
    string Name,
    List<Role> CaseRoles,
    CaseFile CaseFileModel,
    Stage CasePlanModel,
    List<CaseParameter> Inputs = null!,
    List<CaseParameter> Outputs = null!,
    CaseState State = CaseState.Active // Extension: Runtime lifecycle state.
) : CMMNElement();