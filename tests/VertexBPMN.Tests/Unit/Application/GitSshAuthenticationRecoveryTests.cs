using System.Net;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using VertexBPMN.Infrastructure.SourceControl;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Tests.Unit.Application;

public sealed class GitSshAuthenticationRecoveryTests
{
	private const string Key = "-----BEGIN OPENSSH PRIVATE KEY-----\nAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA\n-----END OPENSSH PRIVATE KEY-----\n";
	private static CancellationToken Token => TestContext.Current.CancellationToken;

	[Fact]
	public async Task Restarted_recovery_removes_only_expired_owned_files_after_grace()
	{
		var protection = new EphemeralDataProtectionProvider();
		var recovery = new GitSshAuthenticationFileRecovery(protection);
		var approved = Approved();
		using var lease = new GitSshCredentialLease(approved.Target.Remote, "git", Key, DateTimeOffset.UtcNow.AddMinutes(2));
		await using var files = await GitSshAuthenticationFiles.CreateAsync(approved, lease, recovery, Token);
		var directory = Path.GetDirectoryName(files.KeyFile)!;
		Assert.False(await recovery.PruneDirectoryAsync(directory, DateTimeOffset.UtcNow, Token));
		Assert.False(await recovery.PruneDirectoryAsync(directory, lease.ExpiresAt.AddSeconds(30), Token));
		Assert.True(File.Exists(files.KeyFile));
		var restarted = new GitSshAuthenticationFileRecovery(protection);
		Assert.True(await restarted.PruneDirectoryAsync(directory, lease.ExpiresAt.AddMinutes(2), Token));
		Assert.False(Directory.Exists(directory));
	}

	[Theory]
	[InlineData("ciphertext")]
	[InlineData("format")]
	[InlineData("missing")]
	[InlineData("extra-file")]
	[InlineData("wrong-keys")]
	public async Task Unknown_or_tampered_directories_do_not_authorize_deletion(string mismatch)
	{
		var recovery = new GitSshAuthenticationFileRecovery(new EphemeralDataProtectionProvider());
		var approved = Approved();
		using var lease = new GitSshCredentialLease(approved.Target.Remote, "git", Key, DateTimeOffset.UtcNow.AddMinutes(2));
		await using var files = await GitSshAuthenticationFiles.CreateAsync(approved, lease, recovery, Token);
		var extra = Path.Combine(Path.GetDirectoryName(files.KeyFile)!, "foreign-file");
		try
		{
			if (mismatch == "ciphertext")
			{
				await File.WriteAllTextAsync(files.OwnershipFile, "invalid", Token);
			}
			else if (mismatch == "format")
			{
				await File.WriteAllTextAsync(files.OwnershipFile, "!", Token);
			}
			else if (mismatch == "missing")
			{
				File.Delete(files.OwnershipFile);
			}
			else if (mismatch == "extra-file")
			{
				await File.WriteAllTextAsync(extra, "foreign", Token);
			}
			else
			{
				recovery = new(new EphemeralDataProtectionProvider());
			}
			Assert.False(await recovery.PruneDirectoryAsync(Path.GetDirectoryName(files.KeyFile)!, lease.ExpiresAt.AddMinutes(2), Token));
			Assert.Equal(Key, await File.ReadAllTextAsync(files.KeyFile, Token));
		}
		finally
		{
			File.Delete(extra);
		}
	}

	[Fact]
	public async Task Replayed_ownership_marker_cannot_delete_another_directory()
	{
		var recovery = new GitSshAuthenticationFileRecovery(new EphemeralDataProtectionProvider());
		var approved = Approved();
		using var lease = new GitSshCredentialLease(approved.Target.Remote, "git", Key, DateTimeOffset.UtcNow.AddMinutes(2));
		await using var first = await GitSshAuthenticationFiles.CreateAsync(approved, lease, recovery, Token);
		await using var second = await GitSshAuthenticationFiles.CreateAsync(approved, lease, recovery, Token);
		await File.WriteAllTextAsync(second.OwnershipFile, await File.ReadAllTextAsync(first.OwnershipFile, Token), Token);
		Assert.False(await recovery.PruneDirectoryAsync(Path.GetDirectoryName(second.KeyFile)!, lease.ExpiresAt.AddMinutes(2), Token));
		Assert.True(File.Exists(second.KeyFile));
	}

	[Fact]
	public async Task Cancelled_recovery_leaves_private_key_and_proof_intact()
	{
		var recovery = new GitSshAuthenticationFileRecovery(new EphemeralDataProtectionProvider());
		var approved = Approved();
		using var lease = new GitSshCredentialLease(approved.Target.Remote, "git", Key, DateTimeOffset.UtcNow.AddMinutes(2));
		await using var files = await GitSshAuthenticationFiles.CreateAsync(approved, lease, recovery, Token);
		using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Token);
		await cancellation.CancelAsync();
		await Assert.ThrowsAnyAsync<OperationCanceledException>(() => recovery.PruneDirectoryAsync(Path.GetDirectoryName(files.KeyFile)!,
			lease.ExpiresAt.AddMinutes(2), cancellation.Token));
		Assert.True(File.Exists(files.KeyFile));
		Assert.True(File.Exists(files.OwnershipFile));
	}

	[Fact]
	public async Task Oversized_ownership_marker_is_rejected_without_removing_key()
	{
		var recovery = new GitSshAuthenticationFileRecovery(new EphemeralDataProtectionProvider());
		var approved = Approved();
		using var lease = new GitSshCredentialLease(approved.Target.Remote, "git", Key, DateTimeOffset.UtcNow.AddMinutes(2));
		await using var files = await GitSshAuthenticationFiles.CreateAsync(approved, lease, recovery, Token);
		await File.WriteAllTextAsync(files.OwnershipFile, new string('x', 4097), Token);
		Assert.False(await recovery.PruneDirectoryAsync(Path.GetDirectoryName(files.KeyFile)!, lease.ExpiresAt.AddMinutes(2), Token));
		Assert.True(File.Exists(files.KeyFile));
	}

	[Fact]
	public async Task Cleanup_host_removes_expired_owned_files_even_when_source_control_is_disabled()
	{
		var protection = new EphemeralDataProtectionProvider();
		var recovery = new GitSshAuthenticationFileRecovery(protection);
		var approved = Approved();
		using var lease = new GitSshCredentialLease(approved.Target.Remote, "git", Key, DateTimeOffset.UtcNow.AddMinutes(2));
		await using var files = await GitSshAuthenticationFiles.CreateAsync(approved, lease, recovery, Token);
		var directory = Path.GetDirectoryName(files.KeyFile)!;
		var now = DateTimeOffset.UtcNow;
		var expiredProof = protection.CreateProtector("VertexBPMN.SourceControl.SshAuthFiles.v1", directory).Protect(
			System.Text.Json.JsonSerializer.Serialize(new
			{
				Version = 1,
				CreatedUtcTicks = now.AddMinutes(-4).UtcTicks,
				ExpiresUtcTicks = now.AddMinutes(-2).UtcTicks
			}));
		await File.WriteAllTextAsync(files.OwnershipFile, expiredProof, Token);
		var registrations = new ServiceCollection();
		registrations.AddSingleton<IDataProtectionProvider>(protection);
		registrations.AddSingleton(Options.Create(new SourceControlOptions { Enabled = false }));
		registrations.AddScoped<GitSshAuthenticationFileRecovery>();
		await using var services = registrations.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
		using var host = new SourceControlSshAuthenticationCleanupHostedService(services.GetRequiredService<IServiceScopeFactory>(),
			NullLogger<SourceControlSshAuthenticationCleanupHostedService>.Instance);
		using var deadline = CancellationTokenSource.CreateLinkedTokenSource(Token);
		deadline.CancelAfter(TimeSpan.FromSeconds(15));
		try
		{
			await host.StartAsync(deadline.Token);
			while (Directory.Exists(directory))
			{
				await Task.Delay(TimeSpan.FromMilliseconds(50), deadline.Token);
			}
		}
		finally
		{
			await host.StopAsync(Token);
		}
		Assert.False(services.GetRequiredService<IOptions<SourceControlOptions>>().Value.Enabled);
		Assert.False(File.Exists(files.KeyFile));
	}

	private static ApprovedGitSshTarget Approved()
	{
		var remote = new Uri("ssh://git.example:2222/repositories/models.git");
		var profile = new RepositoryConnectionProfile(Guid.NewGuid(), "tenant", 1, GitTransportKind.Ssh, GitAuthenticationKind.SshKey,
			new(remote, "network", "trust"), "credential", SourceControlCapability.Read);
		return new(new(Guid.NewGuid(), 1, remote, profile, SourceControlCapability.Read),
			new("network", "tenant", remote, [], []),
			new("trust", "tenant", remote, ["ssh-ed25519 YWFhYWFhYWFhYWFhYWFhYQ=="]), [IPAddress.Parse("8.8.8.8")]);
	}
}
