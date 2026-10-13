using VertexBPMN.Application.SourceControl;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Infrastructure.SourceControl;

internal sealed partial class ControlledGitProcess
{
	/// <summary>Fixed-revision SSH fetch; current authority is rechecked throughout the native operation.</summary>
	internal async Task FetchApprovedSshRevisionAsync(GitWorkspace workspace, ApprovedGitSshTarget approved, GitCommitId revision,
		GitSshCredentialLease lease, Func<CancellationToken, Task> demandCurrent, CancellationToken cancellationToken)
		=> _ = await FetchApprovedSshAsync(workspace, approved, revision, 1, lease, demandCurrent, cancellationToken);

	internal async Task FetchApprovedSshHistoryAsync(GitWorkspace workspace, ApprovedGitSshTarget approved, GitCommitId revision,
		GitSshCredentialLease lease, Func<CancellationToken, Task> demandCurrent, CancellationToken cancellationToken)
	{
		GitModelHistory.ValidateLimit(options.Value.Limits);
		_ = await FetchApprovedSshAsync(workspace, approved, revision, options.Value.Limits.MaxHistoryCommits + 1,
			lease, demandCurrent, cancellationToken);
	}

	private Task<byte[]> FetchApprovedSshAsync(GitWorkspace workspace, ApprovedGitSshTarget approved, GitCommitId revision,
		int depth, GitSshCredentialLease lease, Func<CancellationToken, Task> demandCurrent, CancellationToken cancellationToken)
		=> RunApprovedSshAsync(workspace, approved, lease, demandCurrent,
			["fetch", "--no-recurse-submodules", "--no-tags", "--depth=" + depth.ToString(System.Globalization.CultureInfo.InvariantCulture),
				"--", approved.Target.Remote.AbsoluteUri, revision.Value + ":refs/heads/vertex-source"],
			options.Value.Limits.WriteTimeout, cancellationToken);

	private async Task<byte[]> RunApprovedSshAsync(GitWorkspace workspace, ApprovedGitSshTarget approved,
		GitSshCredentialLease lease, Func<CancellationToken, Task> demandCurrent, IReadOnlyList<string> command,
		TimeSpan timeout, CancellationToken cancellationToken, Func<byte[], SourceControlErrorCode>? classifyFailure = null)
	{
		if (approved.Target.RequiredCapabilities is not (SourceControlCapability.Read or SourceControlCapability.Commit or SourceControlCapability.Push) || approved.Target.Profile.Transport != GitTransportKind.Ssh
			|| approved.Target.Remote.AbsoluteUri != lease.Remote.AbsoluteUri)
		{ throw new SourceControlSecurityException(SourceControlErrorCode.ProviderUnavailable); }
		GitSshConfigurationPolicy.ValidateScope(approved.Target.Remote);
		using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		var remaining = lease.ExpiresAt - DateTimeOffset.UtcNow - TimeSpan.FromSeconds(30);
		if (remaining <= TimeSpan.Zero)
		{ throw new SourceControlSecurityException(SourceControlErrorCode.CredentialUnavailable); }
		deadline.CancelAfter(remaining < timeout ? remaining : timeout);
		try
		{
			await demandCurrent(deadline.Token);
			await VersionAsync(workspace, deadline.Token);
			await using var files = await GitSshAuthenticationFiles.CreateAsync(approved, lease,
				sshFiles ?? throw new SourceControlSecurityException(SourceControlErrorCode.ProviderUnavailable), deadline.Token);
			var executable = options.Value.SshExecutablePath ?? throw new SourceControlSecurityException(SourceControlErrorCode.GitUnavailable);
			var sshCommand = files.Command(executable, approved, lease, timeout);
			await using var passphrase = lease.HasPassphrase ? new GitSshPassphraseChannel(files.KeyFile, lease, deadline.Token) : null;
			var environment = new Dictionary<string, string>(StringComparer.Ordinal)
			{ ["GIT_SSH_COMMAND"] = sshCommand, ["GIT_SSH_VARIANT"] = "ssh", ["SSH_AUTH_SOCK"] = "", ["SSH_ASKPASS"] = "", ["SSH_ASKPASS_REQUIRE"] = "never" };
			if (passphrase is not null)
			{
				environment["SSH_ASKPASS"] = CredentialHelperPath();
				environment["SSH_ASKPASS_REQUIRE"] = "force";
				environment["VERTEX_SOURCE_CONTROL_SSH_PASSPHRASE_PIPE"] = passphrase.Name;
			}
			await demandCurrent(deadline.Token);
			var result = await RunAsync(workspace, ["--git-dir=" + Path.Combine(workspace.Directory, "repository.git"), .. command],
				new Dictionary<string, string>(StringComparer.Ordinal) { ["protocol.ssh.allow"] = "always" }, timeout,
				deadline.Token, trustedEnvironment: environment, classifyFailure: classifyFailure);
			try
			{
				await demandCurrent(deadline.Token);
			}
			catch (SourceControlSecurityException) when (command[0] == "push")
			{
				throw new SourceControlSecurityException(SourceControlErrorCode.ResultUnknown);
			}
			return result;
		}
		catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
		{ throw new SourceControlSecurityException(SourceControlErrorCode.TimedOut); }
		catch (OperationCanceledException) { throw; }
		catch (SourceControlSecurityException) { throw; }
		catch { throw new SourceControlSecurityException(SourceControlErrorCode.ProviderUnavailable); }
	}
}
