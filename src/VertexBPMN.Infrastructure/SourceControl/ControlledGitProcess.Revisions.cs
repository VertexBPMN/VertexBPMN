using System.Net;
using VertexBPMN.Application.SourceControl;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Infrastructure.SourceControl;

internal sealed partial class ControlledGitProcess
{
	internal async Task<RevisionSelection> ResolveRemoteBranchAsync(GitWorkspace workspace,
		SourceControlContext context, RepositoryBinding binding, string branch, GitHubTokenLease lease,
		CancellationToken cancellationToken)
	{
		DemandReadBinding(context, binding, branch);
		SourceControlHttps.ValidateTarget(binding.Remote, options.Value.AllowedHosts);
		using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		deadline.CancelAfter(options.Value.Limits.ReadTimeout);
		try
		{
			var addresses = await Dns.GetHostAddressesAsync(binding.Remote.IdnHost, deadline.Token);
			if (addresses.Length == 0 || addresses.Any(address => !SourceControlHttps.IsPublicAddress(address)))
			{
				throw new SourceControlSecurityException(SourceControlErrorCode.InvalidInput);
			}
			return await ResolveRemoteBranchCoreAsync(workspace, binding, branch, lease, addresses, null, deadline.Token);
		}
		catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.TimedOut);
		}
	}

	internal Task<RevisionSelection> ResolveRemoteBranchLocalAcceptanceAsync(GitWorkspace workspace,
		SourceControlContext context, RepositoryBinding binding, string branch, GitHubTokenLease lease,
		string certificateAuthorityFile, CancellationToken cancellationToken)
	{
		DemandReadBinding(context, binding, branch);
		ValidateLocalAcceptanceTarget(binding.Remote, certificateAuthorityFile);
		return ResolveRemoteBranchCoreAsync(workspace, binding, branch, lease, [IPAddress.Loopback], certificateAuthorityFile, cancellationToken);
	}

	private async Task<RevisionSelection> ResolveRemoteBranchCoreAsync(GitWorkspace workspace,
		RepositoryBinding binding, string branch, GitHubTokenLease lease, IPAddress[] addresses,
		string? certificateAuthorityFile, CancellationToken cancellationToken)
	{
		var commit = await ReadRemoteHeadCoreAsync(workspace, binding.Remote, branch, lease, addresses, certificateAuthorityFile, cancellationToken)
			?? throw new SourceControlSecurityException(SourceControlErrorCode.NotFound);
		return new(binding.Id, branch, commit);
	}

	private static void DemandReadBinding(SourceControlContext context, RepositoryBinding binding, string branch)
	{
		if (!string.Equals(context.TenantId, binding.TenantId, StringComparison.Ordinal))
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.NotFound);
		}
		SourceControlInputPolicy.ValidateBranch(branch);
	}

	private async Task<GitCommitId?> ReadRemoteHeadCoreAsync(GitWorkspace workspace, Uri remote, string branch,
		GitHubTokenLease lease, IPAddress[] addresses, string? certificateAuthorityFile, CancellationToken cancellationToken)
	{
		await VersionAsync(workspace, cancellationToken);
		var reference = "refs/heads/" + branch;
		var bytes = await RunAuthenticatedRemoteAsync(workspace, remote, lease, addresses, certificateAuthorityFile,
			["ls-remote", "--quiet", "--branches", "--refs", "--", remote.AbsoluteUri, reference], options.Value.Limits.ReadTimeout, cancellationToken);
		return GitRemoteReferences.ReadHead(bytes, branch);
	}

	private static void ValidateLocalAcceptanceTarget(Uri remote, string certificateAuthorityFile)
	{
		if (!remote.IsAbsoluteUri || remote.Scheme != "https" || remote.Host != "localhost" || remote.Port == 443
			|| remote.UserInfo.Length != 0 || remote.Query.Length != 0 || remote.Fragment.Length != 0
			|| !Path.IsPathFullyQualified(certificateAuthorityFile) || !File.Exists(certificateAuthorityFile))
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.InvalidInput);
		}
	}
}
