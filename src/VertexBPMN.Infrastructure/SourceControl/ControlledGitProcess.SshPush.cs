using System.Text;
using VertexBPMN.Application.SourceControl;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Infrastructure.SourceControl;

internal sealed partial class ControlledGitProcess
{
	internal async Task<GitCommitId?> ReadApprovedSshPushHeadAsync(GitWorkspace workspace, ApprovedGitSshTarget approved,
		PushCommand command, GitSshCredentialLease lease, Func<CancellationToken, Task> demandCurrent, CancellationToken cancellationToken)
	{
		DemandSshPush(approved);
		SourceControlInputPolicy.ValidateBranch(command.WorkBranch);
		var output = await RunApprovedSshAsync(workspace, approved, lease, demandCurrent,
			["ls-remote", "--quiet", "--branches", "--refs", "--", approved.Target.Remote.AbsoluteUri, "refs/heads/" + command.WorkBranch],
			options.Value.Limits.ReadTimeout, cancellationToken);
		return GitRemoteReferences.ReadHead(output, command.WorkBranch);
	}

	internal async Task PushApprovedSshAsync(GitWorkspace workspace, RepositoryBinding binding, ApprovedGitSshTarget approved,
		PushCommand command, GitSshCredentialLease lease, Func<CancellationToken, Task> demandCurrent, CancellationToken cancellationToken)
	{
		DemandSshPush(approved);
		ValidatePush(binding, command);
		if (binding.Id != approved.Target.RepositoryId || binding.Remote.AbsoluteUri != approved.Target.Remote.AbsoluteUri)
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.RevisionConflict);
		}
		var before = await ReadApprovedSshPushHeadAsync(workspace, approved, command, lease, demandCurrent, cancellationToken);
		if (GitPushReconciliation.BeforeFirstWrite(command, before) != GitPushReconciliationResult.ReadyToPush)
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.RevisionConflict);
		}
		await DemandSshPushObjectsAsync(workspace, command, cancellationToken);
		var reference = "refs/heads/" + command.WorkBranch;
		await RunApprovedSshAsync(workspace, approved, lease, demandCurrent,
			["push", "--porcelain", "--no-verify", "--no-follow-tags", "--signed=false", "--recurse-submodules=no",
				"--force-with-lease=" + reference + ":" + (command.ExpectedRemote.Commit?.Value ?? ""),
				"--", binding.Remote.AbsoluteUri, command.Commit.Value + ":" + reference],
			options.Value.Limits.WriteTimeout, cancellationToken,
			output => ClassifyPushFailure(output, command.Commit.Value + ":" + reference));
	}

	private async Task DemandSshPushObjectsAsync(GitWorkspace workspace, PushCommand command, CancellationToken cancellationToken)
	{
		var prefix = "--git-dir=" + Path.Combine(workspace.Directory, "repository.git");
		var commit = await RunAsync(workspace, [prefix, "rev-parse", "--verify", command.Commit.Value + "^{commit}"],
			null, options.Value.Limits.ReadTimeout, cancellationToken);
		if (Encoding.UTF8.GetString(commit).Trim() != command.Commit.Value)
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.ContentUnsafe);
		}
		if (command.ExpectedRemote.Commit is { } expected)
		{
			var ancestor = await RunAsync(workspace, [prefix, "merge-base", expected.Value, command.Commit.Value], null,
				options.Value.Limits.ReadTimeout, cancellationToken, classifyFailure: _ => SourceControlErrorCode.RevisionConflict);
			if (Encoding.UTF8.GetString(ancestor).Trim() != expected.Value)
			{
				throw new SourceControlSecurityException(SourceControlErrorCode.RevisionConflict);
			}
		}
	}

	private static void DemandSshPush(ApprovedGitSshTarget approved)
	{
		if (approved.Target.RequiredCapabilities != SourceControlCapability.Push
			|| (approved.Target.Profile.GitCapabilities & SourceControlCapability.Push) == SourceControlCapability.None)
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.Forbidden);
		}
	}
}
