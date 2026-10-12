using VertexBPMN.SourceControl.Abstractions;
using VertexBPMN.Application.SourceControl;

namespace VertexBPMN.Infrastructure.SourceControl;

internal sealed record AcceptedGitRevisionReadWork(AcceptedGitHttpsExecution Execution, GitCommitId? Revision)
{
	internal AcceptedGitReadQuery? Query { get; init; }
	internal GitCommitId FixedRevision => Revision ?? throw new SourceControlSecurityException(SourceControlErrorCode.ContentUnsafe);
}
