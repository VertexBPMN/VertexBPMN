namespace VertexBPMN.Core.Modeling;

public record CaseFileUpdateEvent(
    string CaseId,
    string CaseFileItemId,
    object NewValue,
    DateTime Timestamp
);