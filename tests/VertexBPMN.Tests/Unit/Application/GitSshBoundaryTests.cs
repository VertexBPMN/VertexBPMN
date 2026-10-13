using System.Net;
using System.Security.AccessControl;
using System.Security.Principal;
using Microsoft.Extensions.Options;
using VertexBPMN.Application.SourceControl;
using VertexBPMN.Infrastructure.SourceControl;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Tests.Unit.Application;

public sealed class GitSshBoundaryTests
{
	private const string SyntheticKey = "-----BEGIN OPENSSH PRIVATE KEY-----\nAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA\n-----END OPENSSH PRIVATE KEY-----\n";
	private static readonly SourceControlContext Context = new("tenant", "actor");
	private static readonly RepositoryConnectionProfile Profile = new(Guid.NewGuid(), "tenant", 1, GitTransportKind.Ssh, GitAuthenticationKind.SshKey,
		new(new Uri("ssh://git.example:2222/repositories/models.git"), "network", "trust"), "credential", SourceControlCapability.Read);
	private static readonly RepositoryBinding Binding = new(Guid.NewGuid(), "tenant", Profile.GitEndpoint.BaseUri, null, "main", "release", ["models"])
	{ ConnectionProfileId = Profile.Id };
	private static readonly SourceControlExecutionTarget Target = new(Binding.Id, 1, Binding.Remote, Profile, SourceControlCapability.Read);
	private static readonly GitSshNetworkPolicy Network = new("network", "tenant", Binding.Remote, [], []);
	private static readonly GitSshTrustProfile Trust = new("trust", "tenant", Binding.Remote, ["ssh-ed25519 YWFhYWFhYWFhYWFhYWFhYQ=="]);

	[Theory]
	[InlineData("127.0.0.1")]
	[InlineData("169.254.169.254")]
	[InlineData("10.1.2.3")]
	[InlineData("::1")]
	[InlineData("fd00:ec2::254")]
	public async Task Every_unsafe_DNS_answer_is_rejected(string address)
	{
		await Assert.ThrowsAsync<SourceControlSecurityException>(() => GitSshNetworkBoundary.ResolveAsync(Context, Binding, Target, Network, Trust,
			(_, _) => Task.FromResult(new[] { IPAddress.Parse("8.8.8.8"), IPAddress.Parse(address) }), TestContext.Current.CancellationToken));
	}

	[Fact]
	public async Task Explicit_private_range_and_port_are_bound_to_the_SSH_profile()
	{
		var approved = await GitSshNetworkBoundary.ResolveAsync(Context, Binding, Target, Network with { AllowedAddressRanges = ["10.1.0.0/16"] }, Trust,
			(_, _) => Task.FromResult(new[] { IPAddress.Parse("10.1.2.3") }), TestContext.Current.CancellationToken);
		Assert.Equal(2222, approved.Target.Remote.Port);
		Assert.Equal("10.1.2.3", Assert.Single(approved.CopyAddresses()).ToString());
	}

	[Theory]
	[InlineData("ssh://git.example:2222/repositories/a;touch.git")]
	[InlineData("ssh://git.example:2222/repositories/a%20b.git")]
	[InlineData("ssh://user@git.example:2222/repositories/models.git")]
	[InlineData("ssh://git.example:2222/repositories/../models.git")]
	public void Shell_paths_userinfo_and_traversal_are_rejected(string input)
	{
		Assert.Throws<SourceControlSecurityException>(() => GitSshConfigurationPolicy.ValidateScope(new(input)));
	}

	[Fact]
	public async Task Authentication_files_are_private_outside_workspace_and_removed_after_use()
	{
		var approved = await GitSshNetworkBoundary.ResolveAsync(Context, Binding, Target, Network, Trust,
			(_, _) => Task.FromResult(new[] { IPAddress.Parse("8.8.8.8") }), TestContext.Current.CancellationToken);
		using var lease = new GitSshCredentialLease(Binding.Remote, "git", SyntheticKey, DateTimeOffset.UtcNow.AddMinutes(2));
		var files = await GitSshAuthenticationFiles.CreateAsync(approved, lease, TestContext.Current.CancellationToken);
		var path = files.KeyFile;
		try
		{
			Assert.True(File.Exists(path));
			Assert.Equal(SyntheticKey, await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken));
			if (OperatingSystem.IsWindows())
			{
				var user = WindowsIdentity.GetCurrent().User!;
				var permissions = new FileInfo(path).GetAccessControl().GetAccessRules(true, true, typeof(SecurityIdentifier)).Cast<FileSystemAccessRule>();
				foreach (var rule in permissions)
				{
					if (rule.AccessControlType == AccessControlType.Allow)
					{ Assert.Equal(user, rule.IdentityReference); }
				}
			}
			else
			{ Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(path)); }
			var command = files.Command(Environment.ProcessPath!, approved, lease, TimeSpan.FromSeconds(5));
			Assert.Contains("HostName=8.8.8.8", command, StringComparison.Ordinal);
			Assert.Contains("IdentityAgent=none", command, StringComparison.Ordinal);
			Assert.Contains("StrictHostKeyChecking=yes", command, StringComparison.Ordinal);
			Assert.DoesNotContain("AAAAAAAA", command, StringComparison.Ordinal);
		}
		finally { await files.DisposeAsync(); }
		Assert.False(File.Exists(path));
	}

	[Fact]
	public void Disposed_key_lease_cannot_be_reused_for_another_host()
	{
		using var lease = new GitSshCredentialLease(Binding.Remote, "git", SyntheticKey, DateTimeOffset.UtcNow.AddMinutes(2));
		Assert.Throws<SourceControlSecurityException>(() => lease.CopyKey(new("ssh://other.example/repositories/models.git")));
		lease.Dispose();
		Assert.Throws<SourceControlSecurityException>(() => lease.CopyKey(Binding.Remote));
		Assert.DoesNotContain("AAAAAAAA", lease.ToString(), StringComparison.Ordinal);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task Internal_deadlines_are_distinct_from_caller_cancellation(bool callerCancels)
	{
		var approved = await GitSshNetworkBoundary.ResolveAsync(Context, Binding, Target, Network, Trust,
			(_, _) => Task.FromResult(new[] { IPAddress.Parse("8.8.8.8") }), TestContext.Current.CancellationToken);
		using var lease = new GitSshCredentialLease(Binding.Remote, "git", SyntheticKey, DateTimeOffset.UtcNow.AddMinutes(2));
		using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
		if (callerCancels)
		{
			await cancellation.CancelAsync();
		}
		var process = new ControlledGitProcess(Options.Create(new SourceControlOptions
		{ Enabled = true, Limits = new() { WriteTimeout = TimeSpan.FromMilliseconds(20) } }));
		async Task FetchAsync() => await process.FetchApprovedSshRevisionAsync(new("unused", "unused", Guid.NewGuid(), 1), approved,
			new(new string('a', 40)), lease, token => Task.Delay(Timeout.InfiniteTimeSpan, token), cancellation.Token);
		if (callerCancels)
		{
			await Assert.ThrowsAnyAsync<OperationCanceledException>(FetchAsync);
		}
		else
		{
			Assert.Equal(SourceControlErrorCode.TimedOut, (await Assert.ThrowsAsync<SourceControlSecurityException>(FetchAsync)).Code);
		}
	}
}
