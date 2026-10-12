using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Infrastructure.SourceControl;

internal sealed record ClaimedReadOutcome<T>(SourceControlOperationState State, T? Value, SourceControlErrorCode? Error)
{
	internal bool CleanupPending { get; init; }
}
