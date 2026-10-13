using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using VertexBPMN.Application.SourceControl;
using VertexBPMN.Infrastructure.SourceControl;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Tests.Unit.Application;

public sealed partial class SourceControlConnectionBindingTests
{
	[Fact]
	public async Task Ssh_read_survives_restart_and_older_workers_cannot_claim_it()
	{
		await using var fixture = await Fixture.OpenAsync(new("ssh://git.example:2222/repositories/models.git"));
		var target = await ReadTargetAsync(fixture);
		var configuration = SshConfiguration(await ApproveSshAsync(fixture));
		var registry = SshExecutionRegistry(() => configuration);
		var request = new AcceptedGitRevisionRead(ReadRevision);
		var id = await fixture.Store.EnqueueSshReadAsync(Context, target, Admin, new("ssh-read"), request, registry, Token);
		Assert.Equal(id, await fixture.Store.EnqueueSshReadAsync(Context, target, Admin, new("ssh-read"), request, registry, Token));
		await using var restarted = fixture.NewContext();
		var store = fixture.NewStore(restarted);
		var work = await store.ReadAcceptedSshReadAsync(Context, id, Admin, registry, Token);
		Assert.Equal(target, work.Execution.Target);
		Assert.Equal(ReadRevision, work.Request.Revision);
		Assert.Equal(configuration.SshTrustProfiles[0].HostKeys, work.Execution.Policy.Trust.HostKeys);
		Assert.Null(await store.TryClaimAsync(Context.TenantId, id, "legacy", DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1), Token));
		await RejectAsync(SourceControlErrorCode.ContentUnsafe, () => store.TryClaimHttpsRevisionReadAsync(Context, id, "https",
			DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1), Admin, ExecutionRegistry(() => new() { Enabled = true }), Token));
		var row = await restarted.SourceControlOperations.SingleAsync(Token);
		Assert.Equal(4, row.ExecutionContractVersion);
		Assert.DoesNotContain("repository-trust", row.ProtectedExecutionTarget, StringComparison.Ordinal);
		Assert.Equal(0, row.Fence);
	}

	[Theory]
	[InlineData("request")]
	[InlineData("network")]
	[InlineData("hostkey")]
	public async Task Ssh_idempotency_binds_content_and_full_policy_snapshot(string change)
	{
		await using var fixture = await Fixture.OpenAsync(new("ssh://git.example:2222/repositories/models.git"));
		var target = await ReadTargetAsync(fixture);
		var configuration = SshConfiguration(await ApproveSshAsync(fixture));
		var registry = SshExecutionRegistry(() => configuration);
		_ = await fixture.Store.EnqueueSshReadAsync(Context, target, Admin, new("ssh-read"), new(ReadRevision), registry, Token);
		configuration = new()
		{
			Enabled = true,
			SshNetworkPolicies = [change == "network" ? configuration.SshNetworkPolicies[0] with { Revision = 2 } : configuration.SshNetworkPolicies[0]],
			SshTrustProfiles = [change == "hostkey" ? configuration.SshTrustProfiles[0] with { HostKeys = ["ssh-ed25519 YmJiYmJiYmJiYmJiYmJiYg=="] } : configuration.SshTrustProfiles[0]]
		};
		await RejectAsync(SourceControlErrorCode.IdempotencyConflict, () => fixture.Store.EnqueueSshReadAsync(Context, target, Admin,
			new("ssh-read"), new(change == "request" ? new(new string('b', 40)) : ReadRevision), registry, Token));
		Assert.Equal(1, await fixture.Db.SourceControlOperations.CountAsync(Token));
	}

	[Theory]
	[InlineData("network", SourceControlErrorCode.RevisionConflict)]
	[InlineData("trust", SourceControlErrorCode.RevisionConflict)]
	[InlineData("revoked-trust", SourceControlErrorCode.ProviderUnavailable)]
	[InlineData("profile", SourceControlErrorCode.ProviderUnavailable)]
	[InlineData("grants", SourceControlErrorCode.NotFound)]
	[InlineData("role", SourceControlErrorCode.NotFound)]
	[InlineData("disabled", SourceControlErrorCode.Disabled)]
	public async Task Ssh_claim_checks_current_authority_before_advancing_fence(string change, SourceControlErrorCode expected)
	{
		await using var fixture = await Fixture.OpenAsync(new("ssh://git.example:2222/repositories/models.git"));
		var target = await ReadTargetAsync(fixture);
		var configuration = SshConfiguration(await ApproveSshAsync(fixture));
		var registry = SshExecutionRegistry(() => configuration);
		var id = await fixture.Store.EnqueueSshReadAsync(Context, target, Admin, new("ssh-read"), new(ReadRevision), registry, Token);
		if (change == "profile")
		{
			Assert.True(await fixture.Store.RevokeConnectionProfileAsync(Context, Admin, fixture.Profile.Id, 1, Token));
		}
		if (change == "grants")
		{
			Assert.True(await fixture.Store.ReplaceGrantsAsync(Context, fixture.Binding.Id, Admin, 1, [], Token));
		}
		configuration = new()
		{
			Enabled = change != "disabled",
			SshNetworkPolicies = [change == "network" ? configuration.SshNetworkPolicies[0] with { DeniedAddressRanges = ["8.8.8.0/24"] } : configuration.SshNetworkPolicies[0]],
			SshTrustProfiles = [configuration.SshTrustProfiles[0] with { Revision = change == "trust" ? 2 : 1, IsRevoked = change == "revoked-trust" }]
		};
		await RejectAsync(expected, () => fixture.Store.TryClaimSshReadAsync(Context, id, "denied", DateTimeOffset.UtcNow,
			TimeSpan.FromMinutes(1), change == "role" ? [] : Admin, registry, Token));
		var row = await fixture.Db.SourceControlOperations.AsNoTracking().SingleAsync(Token);
		Assert.Equal(0, row.Fence);
		Assert.Null(row.LeaseOwner);
	}

	[Fact]
	public async Task Ssh_read_recovery_fences_out_stale_worker_and_shares_quotas()
	{
		await using var fixture = await Fixture.OpenAsync(new("ssh://git.example:2222/repositories/models.git"));
		var registry = SshExecutionRegistry(() => SshConfigurationOptions(fixture));
		var id = await fixture.Store.EnqueueSshReadAsync(Context, await ReadTargetAsync(fixture), Admin, new("ssh-read"), new(ReadRevision), registry, Token);
		Assert.Equal(1, await fixture.Store.TryClaimSshReadAsync(Context, id, "first", DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1), Admin, registry, Token));
		Assert.Null(await fixture.Store.TryClaimSshReadAsync(Context, id, "contender", DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1), Admin, registry, Token));
		await ExpireReadAsync(fixture.Db, id);
		Assert.Equal(2, await fixture.Store.TryClaimSshReadAsync(Context, id, "recovery", DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1), Admin, registry, Token));
		await RejectAsync(SourceControlErrorCode.NotFound, () => fixture.Store.ReadClaimedSshReadAsync(Context, id, "first", 1, Admin, registry, Token));
		Assert.False(await fixture.Store.FinishAsync(Context.TenantId, id, "first", 1, SourceControlOperationState.Succeeded, DateTimeOffset.UtcNow, Token));
		Assert.Equal(ReadRevision, (await fixture.Store.ReadClaimedSshReadAsync(Context, id, "recovery", 2, Admin, registry, Token)).Request.Revision);
		await ExpireReadAsync(fixture.Db, id);
		var active = fixture.Operation(SourceControlOperationState.Running);
		active.LeaseUntilUtcTicks = DateTimeOffset.UtcNow.AddMinutes(2).UtcTicks;
		fixture.Db.SourceControlOperations.Add(active);
		await fixture.Db.SaveChangesAsync(Token);
		var store = fixture.NewStore(fixture.Db, new() { Enabled = true, Limits = new() { MaxConcurrentJobsTotal = 1 } });
		Assert.Null(await store.TryClaimSshReadAsync(Context, id, "quota", DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1), Admin, registry, Token));
	}

	[Theory]
	[InlineData("kind")]
	[InlineData("hash")]
	[InlineData("key")]
	[InlineData("version")]
	[InlineData("ciphertext")]
	[InlineData("request")]
	public async Task Ssh_protected_execution_rejects_retargeting_and_request_substitution(string change)
	{
		await using var fixture = await Fixture.OpenAsync(new("ssh://git.example:2222/repositories/models.git"));
		var registry = SshExecutionRegistry(() => SshConfigurationOptions(fixture));
		var target = await ReadTargetAsync(fixture);
		var id = await fixture.Store.EnqueueSshReadAsync(Context, target, Admin, new("ssh-read"), new(ReadRevision), registry, Token);
		var row = await fixture.Db.SourceControlOperations.SingleAsync(Token);
		switch (change)
		{
			case "kind":
				row.Kind = (int)SourceControlOperationKind.Push;
				break;
			case "hash":
				row.RequestHash = new string('f', 64);
				break;
			case "key":
				row.IdempotencyKey = "changed";
				break;
			case "version":
				row.ExecutionContractVersion = 2;
				break;
			case "ciphertext":
				row.ProtectedExecutionTarget = "invalid";
				break;
			default:
				var other = await fixture.Store.EnqueueSshReadAsync(Context, target, Admin, new("other"), new(new(new string('b', 40))), registry, Token);
				row.ProtectedRequest = (await fixture.Db.SourceControlOperations.SingleAsync(candidate => candidate.Id == other, Token)).ProtectedRequest;
				break;
		}
		await fixture.Db.SaveChangesAsync(Token);
		await using var restarted = fixture.NewContext();
		await RejectAsync(SourceControlErrorCode.ContentUnsafe, () => fixture.NewStore(restarted).ReadAcceptedSshReadAsync(Context, id, Admin, registry, Token));
	}

	private static SourceControlOptions SshConfigurationOptions(Fixture fixture) => new()
	{
		Enabled = true,
		SshNetworkPolicies = [new("repository-network", Context.TenantId, fixture.Binding.Remote, [], [])],
		SshTrustProfiles = [new("repository-trust", Context.TenantId, fixture.Binding.Remote, ["ssh-ed25519 YWFhYWFhYWFhYWFhYWFhYQ=="])]
	};

	private static GitSshPolicyRegistry SshExecutionRegistry(Func<SourceControlOptions> configuration)
	{
		var monitor = new Mock<IOptionsMonitor<SourceControlOptions>>();
		monitor.SetupGet(value => value.CurrentValue).Returns(configuration);
		return new(monitor.Object);
	}
}
