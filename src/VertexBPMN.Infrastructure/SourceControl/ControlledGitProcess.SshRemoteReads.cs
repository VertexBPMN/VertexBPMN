using VertexBPMN.Application.SourceControl;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Infrastructure.SourceControl;

internal sealed partial class ControlledGitProcess
{
	internal async Task<SourceControlPage<string>> ListApprovedSshBranchesAsync(GitWorkspace workspace,
		SourceControlContext context, RepositoryBinding binding, ApprovedGitSshTarget approved, GitSshCredentialLease lease,
		int pageSize, string? cursor, Func<CancellationToken, Task> demandCurrent, CancellationToken cancellationToken)
	{
		DemandSshReadBinding(context, binding, approved);
		GitRemoteReferences.ValidatePage(pageSize, cursor, options.Value.Limits);
		var output = await RunApprovedSshReadAsync(workspace, approved, lease, demandCurrent,
			["ls-remote", "--quiet", "--branches", "--refs", "--", binding.Remote.AbsoluteUri],
			options.Value.Limits.ReadTimeout, cancellationToken);
		return GitRemoteReferences.ReadPage(output, binding, pageSize, cursor, options.Value.Limits, cancellationToken);
	}

	internal async Task<RevisionSelection> ResolveApprovedSshBranchAsync(GitWorkspace workspace,
		SourceControlContext context, RepositoryBinding binding, ApprovedGitSshTarget approved, GitSshCredentialLease lease,
		string branch, Func<CancellationToken, Task> demandCurrent, CancellationToken cancellationToken)
	{
		DemandSshReadBinding(context, binding, approved);
		SourceControlInputPolicy.ValidateBranch(branch);
		var output = await RunApprovedSshReadAsync(workspace, approved, lease, demandCurrent,
			["ls-remote", "--quiet", "--branches", "--refs", "--", binding.Remote.AbsoluteUri, "refs/heads/" + branch],
			options.Value.Limits.ReadTimeout, cancellationToken);
		var commit = GitRemoteReferences.ReadHead(output, branch)
			?? throw new SourceControlSecurityException(SourceControlErrorCode.NotFound);
		return new(binding.Id, branch, commit);
	}

	private static void DemandSshReadBinding(SourceControlContext context, RepositoryBinding binding, ApprovedGitSshTarget approved)
	{
		RepositoryConnectionProfilePolicy.DemandRepository(context, binding, approved.Target.Profile);
		if (binding.Id != approved.Target.RepositoryId || binding.ConnectionProfileId != approved.Target.Profile.Id
			|| binding.Remote.AbsoluteUri != approved.Target.Remote.AbsoluteUri)
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.RevisionConflict);
		}
	}
}
