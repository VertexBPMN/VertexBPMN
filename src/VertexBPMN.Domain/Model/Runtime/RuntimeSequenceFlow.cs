namespace VertexBPMN.Domain.Model.Runtime;

public sealed record RuntimeSequenceFlow(
    string Id,
    string SourceId,
    string TargetId,
    bool IsDefault
);