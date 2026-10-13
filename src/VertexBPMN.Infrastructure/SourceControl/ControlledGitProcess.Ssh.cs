using VertexBPMN.Application.SourceControl;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Infrastructure.SourceControl;

internal sealed partial class ControlledGitProcess
{
	/// <summary>Prepared SSH read transport; durable SSH job dispatch is intentionally not advertised yet.</summary>
	internal async Task FetchApprovedSshRevisionAsync(GitWorkspace workspace, ApprovedGitSshTarget approved, GitCommitId revision,
		GitSshCredentialLease lease, Func<CancellationToken, Task> demandCurrent, CancellationToken cancellationToken)
	{
		if (approved.Target.RequiredCapabilities != SourceControlCapability.Read || approved.Target.Profile.Transport != GitTransportKind.Ssh
			|| approved.Target.Remote.AbsoluteUri != lease.Remote.AbsoluteUri)
		{ throw new SourceControlSecurityException(SourceControlErrorCode.ProviderUnavailable); }
		GitSshConfigurationPolicy.ValidateScope(approved.Target.Remote);
		using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		var remaining = lease.ExpiresAt - DateTimeOffset.UtcNow - TimeSpan.FromSeconds(30);
		if (remaining <= TimeSpan.Zero)
		{ throw new SourceControlSecurityException(SourceControlErrorCode.CredentialUnavailable); }
		deadline.CancelAfter(remaining < options.Value.Limits.WriteTimeout ? remaining : options.Value.Limits.WriteTimeout);
		try
		{
			await demandCurrent(deadline.Token);
			await VersionAsync(workspace, deadline.Token);
			await using var files = await GitSshAuthenticationFiles.CreateAsync(approved, lease, deadline.Token);
			var executable = options.Value.SshExecutablePath ?? throw new SourceControlSecurityException(SourceControlErrorCode.GitUnavailable);
			var command = files.Command(executable, approved, lease, options.Value.Limits.ReadTimeout);
			var environment = new Dictionary<string, string>(StringComparer.Ordinal)
			{ ["GIT_SSH_COMMAND"] = command, ["GIT_SSH_VARIANT"] = "ssh", ["SSH_AUTH_SOCK"] = "", ["SSH_ASKPASS"] = "", ["SSH_ASKPASS_REQUIRE"] = "never" };
			await demandCurrent(deadline.Token);
			await RunAsync(workspace, ["--git-dir=" + Path.Combine(workspace.Directory, "repository.git"), "fetch", "--no-recurse-submodules", "--no-tags",
				"--depth=1", "--", approved.Target.Remote.AbsoluteUri, revision.Value + ":refs/heads/vertex-source"],
				new Dictionary<string, string>(StringComparer.Ordinal) { ["protocol.ssh.allow"] = "always" }, options.Value.Limits.WriteTimeout,
				deadline.Token, trustedEnvironment: environment);
			await demandCurrent(deadline.Token);
		}
		catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
		{ throw new SourceControlSecurityException(SourceControlErrorCode.TimedOut); }
		catch (OperationCanceledException) { throw; }
		catch (SourceControlSecurityException) { throw; }
		catch { throw new SourceControlSecurityException(SourceControlErrorCode.ProviderUnavailable); }
	}
}
