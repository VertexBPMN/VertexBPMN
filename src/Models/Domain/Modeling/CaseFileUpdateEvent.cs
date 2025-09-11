using System;

namespace VertexBPMN.Domain.Modeling;

public record CaseFileUpdateEvent(
    string CaseId,
    string CaseFileItemId,
    object NewValue,
    DateTime Timestamp
);