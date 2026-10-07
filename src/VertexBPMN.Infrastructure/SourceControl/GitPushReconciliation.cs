using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Infrastructure.SourceControl;

internal enum GitPushReconciliationResult
{
	ReadyToPush,
	Confirmed,
	Conflict,
	ResultUnknown
}

/// <summary>Classifies remote evidence; never authorizes replay of an uncertain write.</summary>
internal static class GitPushReconciliation
{
	internal static GitPushReconciliationResult AfterUncertainWrite(GitCommitId target, GitCommitId? remoteHead)
	{
		ArgumentNullException.ThrowIfNull(target);
		return SameCommit(target, remoteHead)
			? GitPushReconciliationResult.Confirmed
			: GitPushReconciliationResult.ResultUnknown;
	}

	internal static GitPushReconciliationResult BeforeFirstWrite(PushCommand command, GitCommitId? remoteHead)
	{
		ArgumentNullException.ThrowIfNull(command);
		ArgumentNullException.ThrowIfNull(command.Commit);
		ArgumentNullException.ThrowIfNull(command.ExpectedRemote);
		// A matching target without this operation's durable write intent is not proof of its execution.
		if (SameCommit(command.Commit, remoteHead))
		{
			return GitPushReconciliationResult.Conflict;
		}

		return SameCommit(command.ExpectedRemote.Commit, remoteHead)
			? GitPushReconciliationResult.ReadyToPush
			: GitPushReconciliationResult.Conflict;
	}

	private static bool SameCommit(GitCommitId? left, GitCommitId? right) =>
		string.Equals(left?.Value, right?.Value, StringComparison.Ordinal);
}
