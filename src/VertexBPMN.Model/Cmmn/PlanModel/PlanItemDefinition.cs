using VertexBPMN.Domain.Model.Cmmn.Core;
using System.Collections.Generic;

namespace VertexBPMN.Domain.Model.Cmmn.PlanModel;

#nullable enable




/// <summary>
/// Abstract plan item definition (Figure 5.6, inherits from CMMNElement).
/// </summary>
public abstract record PlanItemDefinition(
    string Name,
    PlanItemControl? DefaultControl = null
) : CMMNElement();

