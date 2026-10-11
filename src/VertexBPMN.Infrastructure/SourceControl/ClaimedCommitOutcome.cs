using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Infrastructure.SourceControl;

internal sealed record ClaimedCommitOutcome(SourceControlOperationState State, CommitReceipt? Receipt,
    SourceControlErrorCode? Error);
