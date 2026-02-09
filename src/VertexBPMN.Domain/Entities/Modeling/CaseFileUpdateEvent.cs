namespace VertexBPMN.Domain.Entities.Modeling;

public record CaseFileUpdateEvent(
    string CaseId,
    string CaseFileItemId,
    object NewValue,
    DateTime Timestamp
);