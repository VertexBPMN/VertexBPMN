namespace VertexBPMN.Domain.Model.Cmn;

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