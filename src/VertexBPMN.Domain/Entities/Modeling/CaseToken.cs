using System;
using System.Collections.Generic;

namespace VertexBPMN.Domain.Modeling;

public record struct CaseToken(
    Guid Id,
    Guid CaseInstanceId,
    string CurrentPlanItemId,
    string PlanItemType,
    Dictionary<string, object> CaseFile,
    DateTime CreatedAt,
    string? AssignedWorker = null,
    DateTime? AssignedAt = null
);