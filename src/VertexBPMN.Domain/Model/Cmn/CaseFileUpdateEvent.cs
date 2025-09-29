namespace VertexBPMN.Domain.Model.Cmn;

public record CaseFileUpdateEvent(
    string CaseId,
    string CaseFileItemId,
    object NewValue,
    DateTime Timestamp
);