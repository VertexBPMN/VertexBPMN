using System.Net;
using System.Text;
using VertexBPMN.Application.SourceControl;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Infrastructure.SourceControl;

internal sealed partial class ControlledGitProcess
{
	internal Task<GitCommitId?> ReadPushHeadAsync(GitWorkspace workspace, RepositoryBinding binding,
		PushCommand command, GitHubTokenLease lease, CancellationToken cancellationToken) =>
		WithPushRemoteAsync(workspace, binding, command, lease,
			(addresses, token) => ReadRemoteHeadCoreAsync(workspace, binding.Remote, command.WorkBranch, lease, addresses, null, token), cancellationToken);

	internal async Task PushAsync(GitWorkspace workspace, RepositoryBinding binding, PushCommand command,
		GitHubTokenLease lease, CancellationToken cancellationToken) =>
		_ = await WithPushRemoteAsync(workspace, binding, command, lease, async (addresses, token) =>
		{
			await PushCoreAsync(workspace, binding, command, lease, addresses, null, token);
			return true;
		}, cancellationToken);

	internal Task<GitCommitId?> ReadPushHeadLocalAcceptanceAsync(GitWorkspace workspace, RepositoryBinding binding,
		PushCommand command, GitHubTokenLease lease, string certificateAuthorityFile, CancellationToken cancellationToken)
	{
		ValidatePush(binding, command);
		ValidateLocalAcceptanceTarget(binding.Remote, certificateAuthorityFile);
		return ReadRemoteHeadCoreAsync(workspace, binding.Remote, command.WorkBranch, lease, [IPAddress.Loopback], certificateAuthorityFile, cancellationToken);
	}

	internal Task PushLocalAcceptanceAsync(GitWorkspace workspace, RepositoryBinding binding, PushCommand command,
		GitHubTokenLease lease, string certificateAuthorityFile, CancellationToken cancellationToken)
	{
		ValidatePush(binding, command);
		ValidateLocalAcceptanceTarget(binding.Remote, certificateAuthorityFile);
		return PushCoreAsync(workspace, binding, command, lease, [IPAddress.Loopback], certificateAuthorityFile, cancellationToken);
	}

	private async Task<T> WithPushRemoteAsync<T>(GitWorkspace workspace, RepositoryBinding binding, PushCommand command,
		GitHubTokenLease lease, Func<IPAddress[], CancellationToken, Task<T>> execute, CancellationToken cancellationToken)
	{
		ValidatePush(binding, command);
		SourceControlHttps.ValidateTarget(binding.Remote, options.Value.AllowedHosts);
		using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		deadline.CancelAfter(options.Value.Limits.WriteTimeout);
		try
		{
			await VersionAsync(workspace, deadline.Token);
			var addresses = await Dns.GetHostAddressesAsync(binding.Remote.IdnHost, deadline.Token);
			if (addresses.Length == 0 || addresses.Any(address => !SourceControlHttps.IsPublicAddress(address)))
			{
				throw new SourceControlSecurityException(SourceControlErrorCode.InvalidInput);
			}
			return await execute(addresses, deadline.Token);
		}
		catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.TimedOut);
		}
	}

	private async Task PushCoreAsync(GitWorkspace workspace, RepositoryBinding binding, PushCommand command,
		GitHubTokenLease lease, IPAddress[] addresses, string? caFile, CancellationToken cancellationToken)
	{
		await VersionAsync(workspace, cancellationToken);
		var before = await ReadRemoteHeadCoreAsync(workspace, binding.Remote, command.WorkBranch, lease, addresses, caFile, cancellationToken);
		if (GitPushReconciliation.BeforeFirstWrite(command, before) != GitPushReconciliationResult.ReadyToPush)
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.RevisionConflict);
		}
		var prefix = "--git-dir=" + Path.Combine(workspace.Directory, "repository.git");
		var commit = await RunAsync(workspace, [prefix, "rev-parse", "--verify", command.Commit.Value + "^{commit}"],
			null, options.Value.Limits.ReadTimeout, cancellationToken);
		if (Encoding.UTF8.GetString(commit).Trim() != command.Commit.Value)
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.ContentUnsafe);
		}
		if (command.ExpectedRemote.Commit is { } expected)
		{
			// Explicit lease alone permits a forced update. Prove fast-forward first, using immutable OIDs.
			var ancestor = await RunAsync(workspace, [prefix, "merge-base", expected.Value, command.Commit.Value],
				null, options.Value.Limits.ReadTimeout, cancellationToken,
				classifyFailure: _ => SourceControlErrorCode.RevisionConflict);
			if (Encoding.UTF8.GetString(ancestor).Trim() != expected.Value)
			{
				throw new SourceControlSecurityException(SourceControlErrorCode.RevisionConflict);
			}
		}
		var reference = "refs/heads/" + command.WorkBranch;
		// Exact expected OID (or absence), one ref, no '+', no unrestricted force, no ambient tracking ref.
		await RunAuthenticatedRemoteAsync(workspace, binding.Remote, lease, addresses, caFile,
			["push", "--porcelain", "--no-verify", "--no-follow-tags", "--signed=false", "--recurse-submodules=no",
				"--force-with-lease=" + reference + ":" + (command.ExpectedRemote.Commit?.Value ?? ""),
				"--", binding.Remote.AbsoluteUri, command.Commit.Value + ":" + reference],
			options.Value.Limits.WriteTimeout, cancellationToken, output => ClassifyPushFailure(output, command.Commit.Value + ":" + reference));
	}

	private static SourceControlErrorCode ClassifyPushFailure(byte[] output, string refspec)
	{
		// Only a complete explicit rejection proves conflict. Network/auth/partial output stays uncertain.
		var text = Encoding.UTF8.GetString(output);
		var lines = text.Split('\n');
		return lines.Take(lines.Length - 1).Any(line => line.TrimEnd('\r') == "!\t" + refspec + "\t[rejected] (stale info)"
			|| line.TrimEnd('\r') == "!\t" + refspec + "\t[remote rejected] (failed to update ref)"
			|| line.TrimEnd('\r') == "!\t" + refspec + "\t[remote rejected] (incorrect old value provided)"
			|| line.TrimEnd('\r') == "!\t" + refspec + "\t[remote rejected] (reference already exists)")
			? SourceControlErrorCode.RevisionConflict : SourceControlErrorCode.ProviderUnavailable;
	}

	private static void ValidatePush(RepositoryBinding binding, PushCommand command)
	{
		ArgumentNullException.ThrowIfNull(command);
		SourceControlInputPolicy.ValidateBranch(command.WorkBranch);
		var suffix = command.WorkBranch.StartsWith("vertex/", StringComparison.Ordinal) ? command.WorkBranch[7..] : "";
		if (command.OperationId == Guid.Empty || command.Commit is null || command.ExpectedRemote is null
			|| !Guid.TryParseExact(suffix, "N", out var session) || session == Guid.Empty
			|| command.WorkBranch != SourceControlInputPolicy.WorkBranch(session)
			|| string.Equals(command.WorkBranch, binding.DefaultBranch, StringComparison.OrdinalIgnoreCase)
			|| string.Equals(command.WorkBranch, binding.ReleaseBranch, StringComparison.OrdinalIgnoreCase)
			|| command.Commit.Value.All(character => character == '0')
			|| command.ExpectedRemote.Commit?.Value.All(character => character == '0') == true)
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.InvalidInput);
		}
	}

}
