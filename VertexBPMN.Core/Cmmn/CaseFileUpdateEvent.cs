namespace VertexBPMN.Core.Cmmn;

public record CaseFileUpdateEvent(
    string CaseId,
    string CaseFileItemId,
    object NewValue,
    DateTime Timestamp
);