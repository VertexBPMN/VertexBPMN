using VertexBPMN.Infrastructure.SourceControl;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.SourceControl.Tests;

public sealed class GitPushReconciliationTests
{
	private static readonly GitCommitId Target = new(new string('1', 40));
	private static readonly GitCommitId Previous = new(new string('2', 40));
	private static readonly GitCommitId Other = new(new string('3', 40));

	[Fact]
	public void Lost_response_is_confirmed_only_by_exact_target_head()
	{
		Assert.Equal(GitPushReconciliationResult.Confirmed, GitPushReconciliation.AfterUncertainWrite(Target, Target));
		foreach (var head in new GitCommitId?[] { null, Previous, Other })
		{
			Assert.Equal(GitPushReconciliationResult.ResultUnknown, GitPushReconciliation.AfterUncertainWrite(Target, head));
		}
	}

	[Fact]
	public void First_write_requires_exact_expected_remote_head()
	{
		var command = Command(ExpectedRemoteRef.At(Previous));
		Assert.Equal(GitPushReconciliationResult.ReadyToPush, GitPushReconciliation.BeforeFirstWrite(command, Previous));
		foreach (var head in new GitCommitId?[] { null, Target, Other })
		{
			Assert.Equal(GitPushReconciliationResult.Conflict, GitPushReconciliation.BeforeFirstWrite(command, head));
		}
	}

	[Fact]
	public void Branch_creation_requires_absence_and_does_not_claim_unowned_target()
	{
		var command = Command(ExpectedRemoteRef.Absent);
		Assert.Equal(GitPushReconciliationResult.ReadyToPush, GitPushReconciliation.BeforeFirstWrite(command, null));
		Assert.Equal(GitPushReconciliationResult.Conflict, GitPushReconciliation.BeforeFirstWrite(command, Target));
		Assert.Equal(GitPushReconciliationResult.Conflict, GitPushReconciliation.BeforeFirstWrite(command, Other));
	}

	private static PushCommand Command(ExpectedRemoteRef expected) =>
		new(Guid.NewGuid(), new("push-reconciliation"), "vertex/test", Target, expected);
}
