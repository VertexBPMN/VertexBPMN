using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using VertexBPMN.Application.SourceControl;
using VertexBPMN.Infrastructure.SourceControl;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Tests.Unit.Application;

public sealed partial class SourceControlConnectionBindingTests
{
	private static readonly GitCommitId ReadRevision = new(new string('a', 40));

	[Fact]
	public async Task Dedicated_read_claim_reconstructs_exact_revision_and_fences_out_an_old_worker()
	{
		await using var fixture = await Fixture.OpenAsync();
		Assert.True(await fixture.AssignAsync());
		var registry = ExecutionRegistry(() => HttpsPreparationOptions(fixture));
		var id = await AcceptRevisionReadAsync(fixture, registry);
		await using var restarted = fixture.NewContext();
		var store = fixture.NewStore(restarted);
		var now = DateTimeOffset.UtcNow;
		Assert.Null(await store.TryClaimAsync(Context.TenantId, id, "legacy", now, TimeSpan.FromMinutes(1), Token));
		Assert.Equal(1, await store.TryClaimHttpsRevisionReadAsync(Context, id, "first", now, TimeSpan.FromMinutes(1), Admin, registry, Token));
		Assert.Null(await store.TryClaimHttpsRevisionReadAsync(Context, id, "contender", now, TimeSpan.FromMinutes(1), Admin, registry, Token));
		Assert.Equal(ReadRevision, (await store.ReadClaimedHttpsRevisionReadAsync(Context, id, "first", 1, Admin, registry, Token)).Revision);
		await ExpireReadAsync(restarted, id);
		Assert.Equal(2, await store.TryClaimHttpsRevisionReadAsync(Context, id, "recovery", DateTimeOffset.UtcNow,
			TimeSpan.FromMinutes(1), Admin, registry, Token));
		await RejectAsync(SourceControlErrorCode.NotFound, () => store.ReadClaimedHttpsRevisionReadAsync(Context, id, "first", 1, Admin, registry, Token));
		Assert.False(await store.FinishAsync(Context.TenantId, id, "first", 1, SourceControlOperationState.Succeeded, DateTimeOffset.UtcNow, Token));
		Assert.Equal(ReadRevision, (await store.ReadClaimedHttpsRevisionReadAsync(Context, id, "recovery", 2, Admin, registry, Token)).Revision);
		Assert.True(await store.FinishAsync(Context.TenantId, id, "recovery", 2, SourceControlOperationState.Succeeded, DateTimeOffset.UtcNow, Token));
		Assert.Null(await store.TryClaimHttpsRevisionReadAsync(Context, id, "finished", DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1), Admin, registry, Token));
	}

	[Theory]
	[InlineData(SourceControlOperationState.ResultUnknown)]
	[InlineData(SourceControlOperationState.Reconciling)]
	public async Task Expired_private_reads_can_be_replayed_without_promoting_a_write(SourceControlOperationState state)
	{
		await using var fixture = await Fixture.OpenAsync();
		Assert.True(await fixture.AssignAsync());
		var registry = ExecutionRegistry(() => HttpsPreparationOptions(fixture));
		var id = await AcceptRevisionReadAsync(fixture, registry);
		await fixture.Db.SourceControlOperations.Where(row => row.Id == id)
			.ExecuteUpdateAsync(update => update.SetProperty(row => row.State, (int)state).SetProperty(row => row.Fence, 8), Token);
		Assert.Equal(9, await fixture.Store.TryClaimHttpsRevisionReadAsync(Context, id, "replay", DateTimeOffset.UtcNow,
			TimeSpan.FromMinutes(1), Admin, registry, Token));
		Assert.Equal(ReadRevision, (await fixture.Store.ReadClaimedHttpsRevisionReadAsync(Context, id, "replay", 9, Admin, registry, Token)).Revision);
	}

	[Theory]
	[InlineData("network")]
	[InlineData("grants")]
	[InlineData("profile")]
	[InlineData("role")]
	public async Task Dedicated_claim_rechecks_authority_before_changing_the_fence(string change)
	{
		await using var fixture = await Fixture.OpenAsync();
		Assert.True(await fixture.AssignAsync());
		var configuration = HttpsPreparationOptions(fixture);
		var registry = ExecutionRegistry(() => configuration);
		var id = await AcceptRevisionReadAsync(fixture, registry);
		configuration = await ChangeAcceptedReadContextAsync(fixture, configuration, change == "network" ? "policy" : change);
		await Assert.ThrowsAsync<SourceControlSecurityException>(() => fixture.Store.TryClaimHttpsRevisionReadAsync(Context, id,
			"denied", DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1), change == "role" ? [] : Admin, registry, Token));
		var row = await fixture.Db.SourceControlOperations.AsNoTracking().SingleAsync(Token);
		Assert.Equal(0, row.Fence);
		Assert.Null(row.LeaseOwner);
		Assert.Equal((int)SourceControlOperationState.Queued, row.State);
	}

	[Theory]
	[InlineData("total")]
	[InlineData("tenant")]
	public async Task Dedicated_read_claim_preserves_shared_job_quotas(string quota)
	{
		await using var fixture = await Fixture.OpenAsync();
		Assert.True(await fixture.AssignAsync());
		var registry = ExecutionRegistry(() => HttpsPreparationOptions(fixture));
		var id = await AcceptRevisionReadAsync(fixture, registry);
		var active = fixture.Operation(SourceControlOperationState.Running);
		active.LeaseUntilUtcTicks = DateTimeOffset.UtcNow.AddMinutes(2).UtcTicks;
		active.TenantId = quota == "total" ? "other-tenant" : Context.TenantId;
		fixture.Db.SourceControlOperations.Add(active);
		await fixture.Db.SaveChangesAsync(Token);
		var configuration = new SourceControlOptions
		{
			Enabled = true,
			Limits = new() { MaxConcurrentJobsTotal = quota == "total" ? 1 : 8, MaxConcurrentJobsPerTenant = quota == "tenant" ? 1 : 8 }
		};
		Assert.Null(await fixture.NewStore(fixture.Db, configuration).TryClaimHttpsRevisionReadAsync(Context, id, "quota",
			DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1), Admin, registry, Token));
		Assert.Equal(0, (await fixture.Db.SourceControlOperations.AsNoTracking().SingleAsync(row => row.Id == id, Token)).Fence);
	}

	[Fact]
	public async Task Raw_read_envelopes_are_not_executable_revision_requests()
	{
		await using var fixture = await Fixture.OpenAsync();
		Assert.True(await fixture.AssignAsync());
		var registry = ExecutionRegistry(() => HttpsPreparationOptions(fixture));
		var id = await AcceptReadAsync(fixture, await ReadTargetAsync(fixture), registry);
		await RejectAsync(SourceControlErrorCode.ContentUnsafe, () => fixture.Store.TryClaimHttpsRevisionReadAsync(Context, id,
			"raw", DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1), Admin, registry, Token));
		Assert.Equal(0, (await fixture.Db.SourceControlOperations.AsNoTracking().SingleAsync(Token)).Fence);
	}

	[Theory]
	[InlineData("network")]
	[InlineData("role")]
	[InlineData("expiry")]
	[InlineData("stolen")]
	[InlineData("cancel")]
	public async Task Claimed_transport_rejects_context_changes_during_dns_without_resolving_credentials(string change)
	{
		await using var fixture = await Fixture.OpenAsync();
		Assert.True(await fixture.AssignAsync());
		var configuration = HttpsPreparationOptions(fixture);
		var registry = ExecutionRegistry(() => configuration);
		var id = await AcceptRevisionReadAsync(fixture, registry);
		Assert.Equal(1, await fixture.Store.TryClaimHttpsRevisionReadAsync(Context, id, "dns", DateTimeOffset.UtcNow,
			TimeSpan.FromMinutes(1), Admin, registry, Token));
		var actors = new CredentialActors();
		var credentials = new ProfileCredentials();
		var transport = PreparationTransport(fixture, actors, credentials, () => configuration);
		using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Token);
		async Task<IPAddress[]> ResolveAsync(string host, CancellationToken token)
		{
			Assert.Equal(fixture.Binding.Remote.IdnHost, host);
			if (change == "network")
			{
				configuration = HttpsPreparationOptions(fixture, configuration.HttpsNetworkPolicies[0] with { Revision = 2 });
			}
			if (change == "role")
			{
				actors.Roles = [];
			}
			if (change == "expiry")
			{
				await ExpireReadAsync(fixture.Db, id);
			}
			if (change == "stolen")
			{
				await fixture.Db.SourceControlOperations.Where(row => row.Id == id)
					.ExecuteUpdateAsync(update => update.SetProperty(row => row.LeaseOwner, "new-owner").SetProperty(row => row.Fence, 2), token);
			}
			if (change == "cancel")
			{
				await cancellation.CancelAsync();
			}
			return [IPAddress.Parse("8.8.8.8")];
		}
		if (change == "cancel")
		{
			await Assert.ThrowsAnyAsync<OperationCanceledException>(() => transport.PrepareClaimedRevisionReadAsync(Context, id, "dns", 1, ResolveAsync, cancellation.Token));
		}
		else
		{
			await Assert.ThrowsAsync<SourceControlSecurityException>(() => transport.PrepareClaimedRevisionReadAsync(Context, id, "dns", 1, ResolveAsync, cancellation.Token));
		}
		Assert.Empty(credentials.Requests);
		Assert.Equal(0, credentials.MetadataCalls);
	}

	[Fact]
	public async Task Claimed_transport_uses_one_dns_snapshot_and_exact_accepted_revision()
	{
		await using var fixture = await Fixture.OpenAsync();
		Assert.True(await fixture.AssignAsync());
		var configuration = HttpsPreparationOptions(fixture);
		var registry = ExecutionRegistry(() => configuration);
		var id = await AcceptRevisionReadAsync(fixture, registry);
		Assert.Equal(1, await fixture.Store.TryClaimHttpsRevisionReadAsync(Context, id, "read", DateTimeOffset.UtcNow,
			TimeSpan.FromMinutes(1), Admin, registry, Token));
		var credentials = new ProfileCredentials();
		var transport = PreparationTransport(fixture, new(), credentials, () => configuration);
		var calls = 0;
		var prepared = await transport.PrepareClaimedRevisionReadAsync(Context, id, "read", 1, (_, _) =>
		{
			calls++;
			return Task.FromResult(new[] { IPAddress.Parse("8.8.8.8") });
		}, Token);
		Assert.Equal(1, calls);
		Assert.Equal(ReadRevision, prepared.Work.Revision);
		Assert.Equal("8.8.8.8", Assert.Single(prepared.Approved.CopyAddresses()).ToString());
		Assert.Equal(0, credentials.MetadataCalls);
	}

	[Theory]
	[InlineData("missing-version")]
	[InlineData("version")]
	[InlineData("null-revision")]
	[InlineData("oid")]
	[InlineData("expression")]
	[InlineData("oversized")]
	public async Task Dedicated_claim_rejects_malformed_or_unsupported_revision_contracts(string invalid)
	{
		await using var fixture = await Fixture.OpenAsync();
		Assert.True(await fixture.AssignAsync());
		var registry = ExecutionRegistry(() => HttpsPreparationOptions(fixture));
		var revisionValue = invalid switch
		{
			"oid" => "main",
			"expression" => ReadRevision.Value + "~1",
			_ => ReadRevision.Value
		};
		var request = invalid switch
		{
			"missing-version" => JsonSerializer.SerializeToUtf8Bytes(new { Revision = ReadRevision }),
			"null-revision" => Encoding.UTF8.GetBytes("{\"SchemaVersion\":1,\"Revision\":null}"),
			"oversized" => Encoding.UTF8.GetBytes(new string(' ', 3000)),
			_ => JsonSerializer.SerializeToUtf8Bytes(new
			{
				SchemaVersion = invalid == "version" ? 2 : 1,
				Revision = new { Value = revisionValue }
			})
		};
		var id = await AcceptReadAsync(fixture, await ReadTargetAsync(fixture), registry, request);
		await RejectAsync(SourceControlErrorCode.ContentUnsafe, () => fixture.Store.TryClaimHttpsRevisionReadAsync(Context, id,
			"invalid", DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1), Admin, registry, Token));
		Assert.Equal(0, (await fixture.Db.SourceControlOperations.AsNoTracking().SingleAsync(Token)).Fence);
	}

	[Theory]
	[InlineData("policy")]
	[InlineData("expiry")]
	[InlineData("wrong-worker")]
	public async Task Claimed_transport_rejects_stale_context_before_dns(string change)
	{
		await using var fixture = await Fixture.OpenAsync();
		Assert.True(await fixture.AssignAsync());
		var configuration = HttpsPreparationOptions(fixture);
		var registry = ExecutionRegistry(() => configuration);
		var id = await AcceptRevisionReadAsync(fixture, registry);
		Assert.Equal(1, await fixture.Store.TryClaimHttpsRevisionReadAsync(Context, id, "read", DateTimeOffset.UtcNow,
			TimeSpan.FromMinutes(1), Admin, registry, Token));
		configuration = await ChangeAcceptedReadContextAsync(fixture, configuration, change);
		if (change == "expiry")
		{
			await ExpireReadAsync(fixture.Db, id);
		}
		var credentials = new ProfileCredentials();
		var transport = PreparationTransport(fixture, new(), credentials, () => configuration);
		var calls = 0;
		await Assert.ThrowsAsync<SourceControlSecurityException>(() => transport.PrepareClaimedRevisionReadAsync(Context, id,
			change == "wrong-worker" ? "other" : "read", 1, (_, _) =>
			{
				calls++;
				return Task.FromResult(new[] { IPAddress.Parse("8.8.8.8") });
			}, Token));
		Assert.Equal(0, calls);
		Assert.Equal(0, credentials.MetadataCalls);
	}

	[Theory]
	[InlineData("", 1)]
	[InlineData("worker", 0)]
	[InlineData("worker", 6)]
	[InlineData("bad\nworker", 1)]
	public async Task Invalid_claim_identity_or_duration_cannot_change_the_fence(string worker, int minutes)
	{
		await using var fixture = await Fixture.OpenAsync();
		Assert.True(await fixture.AssignAsync());
		var registry = ExecutionRegistry(() => HttpsPreparationOptions(fixture));
		var id = await AcceptRevisionReadAsync(fixture, registry);
		await RejectAsync(SourceControlErrorCode.InvalidInput, () => fixture.Store.TryClaimHttpsRevisionReadAsync(Context, id,
			worker, DateTimeOffset.UtcNow, TimeSpan.FromMinutes(minutes), Admin, registry, Token));
		Assert.Equal(0, (await fixture.Db.SourceControlOperations.AsNoTracking().SingleAsync(Token)).Fence);
	}

	[Theory]
	[InlineData("operation")]
	[InlineData("fence")]
	public async Task Fetch_cannot_use_a_workspace_from_another_operation_or_claim(string change)
	{
		await using var fixture = await Fixture.OpenAsync();
		var credentials = new ProfileCredentials();
		var transport = PreparationTransport(fixture, new(), credentials, () => HttpsPreparationOptions(fixture));
		var id = Guid.NewGuid();
		var workspace = new GitWorkspace("unused", "unused", change == "operation" ? Guid.NewGuid() : id, change == "fence" ? 2 : 1);
		await RejectAsync(SourceControlErrorCode.NotFound, () => transport.FetchClaimedRevisionReadAsync(workspace, Context, id, "worker", 1, Token));
		Assert.Equal(0, credentials.MetadataCalls);
	}

	private static async Task<Guid> AcceptRevisionReadAsync(Fixture fixture, GitHttpsPolicyRegistry registry) =>
		await fixture.Store.EnqueueProfiledRevisionReadAsync(Context, await ReadTargetAsync(fixture), Admin,
			new("accepted-revision-read"), ReadRevision, registry, Token);

	private static Task<int> ExpireReadAsync(VertexBPMN.Infrastructure.Persistence.BpmnDbContext db, Guid id) =>
		db.SourceControlOperations.Where(row => row.Id == id).ExecuteUpdateAsync(update => update
			.SetProperty(row => row.LeaseUntilUtcTicks, DateTimeOffset.UtcNow.AddSeconds(-1).UtcTicks), Token);
}
