using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using VertexBPMN.Application.SourceControl;
using VertexBPMN.Infrastructure.SourceControl;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Tests.Unit.Application;

public sealed partial class SourceControlConnectionBindingTests
{
	[Fact]
	public async Task Ssh_commit_acceptance_survives_restart_and_fences_an_expired_worker()
	{
		await using var fixture = await Fixture.OpenAsync(new("ssh://git.example:2222/repositories/models.git"));
		var (store, id, submission) = await AcceptProfiledCommitAsync(fixture);
		Assert.Equal(id, await store.EnqueueCommitAsync(Context, fixture.Binding.Id, Admin, new("profiled-commit"), submission, Token));
		Assert.Null(await store.TryClaimAsync(Context.TenantId, id, "legacy", DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1), Token));
		Assert.Equal(1, await store.TryClaimProfiledWriteAsync(Context, id, "first", DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1), Admin, Token));
		var execution = await store.ReadClaimedWriteExecutionAsync(Context, id, "first", 1, Admin, Token);
		Assert.Equal(5, execution.ContractVersion);
		Assert.NotNull(execution.Ssh);
		Assert.Null(execution.Https);
		await ExpireReadAsync(fixture.Db, id);
		await using var restarted = fixture.NewContext();
		var recovered = fixture.NewStore(restarted, SshConfigurationOptions(fixture));
		Assert.Equal(2, await recovered.TryClaimProfiledWriteAsync(Context, id, "restart", DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1), Admin, Token));
		var work = await recovered.ReadCommitWorkAsync(Context, id, "restart", 2, Admin, Token);
		Assert.Equal(SourceControlOperationState.Reconciling, work.State);
		Assert.Equal(submission.Snapshots[0].CopyContent(), Assert.Single(work.Command.Snapshots).CopyContent());
		await RejectAsync(SourceControlErrorCode.NotFound, () => recovered.ReadClaimedWriteExecutionAsync(Context, id, "first", 1, Admin, Token));
	}

	[Theory]
	[InlineData("role")]
	[InlineData("trust")]
	[InlineData("network")]
	[InlineData("ciphertext")]
	[InlineData("kind")]
	public async Task Ssh_write_claim_rejects_changed_authority_or_protected_evidence(string change)
	{
		await using var fixture = await Fixture.OpenAsync(new("ssh://git.example:2222/repositories/models.git"));
		var (store, id, _) = await AcceptProfiledCommitAsync(fixture);
		var configuration = SshConfigurationOptions(fixture);
		configuration = new()
		{
			Enabled = true,
			SshTrustProfiles = [configuration.SshTrustProfiles[0] with { Revision = change == "trust" ? 2 : 1 }],
			SshNetworkPolicies = [configuration.SshNetworkPolicies[0] with { Revision = change == "network" ? 2 : 1 }]
		};
		if (change == "ciphertext")
		{
			await fixture.Db.SourceControlOperations.Where(row => row.Id == id)
				.ExecuteUpdateAsync(update => update.SetProperty(row => row.ProtectedExecutionTarget, "invalid"), Token);
		}
		if (change == "kind")
		{
			await fixture.Db.SourceControlOperations.Where(row => row.Id == id)
				.ExecuteUpdateAsync(update => update.SetProperty(row => row.Kind, (int)SourceControlOperationKind.Push), Token);
		}
		var current = change is "trust" or "network" ? fixture.NewStore(fixture.Db, configuration) : store;
		await Assert.ThrowsAsync<SourceControlSecurityException>(() => current.TryClaimProfiledWriteAsync(Context, id, "denied",
			DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1), change == "role" ? [] : Admin, Token));
		Assert.Equal(0, (await fixture.Db.SourceControlOperations.AsNoTracking().SingleAsync(Token)).Fence);
	}

	[Fact]
	public async Task Ssh_push_copies_confirmed_commit_and_preserves_intent_on_recovery()
	{
		await using var fixture = await Fixture.OpenAsync(new("ssh://git.example:2222/repositories/models.git"));
		var (store, id, submission) = await AcceptProfiledCommitAsync(fixture);
		Assert.Equal(1, await store.TryClaimProfiledWriteAsync(Context, id, "commit", DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1), Admin, Token));
		var snapshot = submission.Snapshots[0];
		var receipt = new CommitReceipt(id, submission.SessionId, new(new string('b', 40)), submission.WorkBranch,
			[new(snapshot.Path, snapshot.DocumentGeneration, snapshot.LocalRevision, snapshot.ContentSha256)]);
		Assert.True(await store.SaveResultAsync(Context.TenantId, id, "commit", 1,
			JsonSerializer.SerializeToUtf8Bytes(new StoredLocalCommit(1, fixture.Binding.Id, 1, receipt)), DateTimeOffset.UtcNow, Token));
		Assert.True(await store.FinishAsync(Context.TenantId, id, "commit", 1, SourceControlOperationState.CommittedLocal, DateTimeOffset.UtcNow, Token));
		var audited = store.ReadCompletedAudit(await fixture.Db.SourceControlOperations.AsNoTracking().SingleAsync(row => row.Id == id, Token));
		Assert.Equal(Context.ActorId, audited.UserId);
		Assert.Contains(fixture.Profile.Id.ToString("D"), audited.DetailsJson, StringComparison.Ordinal);
		Assert.Contains("new-reference", audited.DetailsJson, StringComparison.Ordinal);
		var push = await store.EnqueuePushAsync(Context, fixture.Binding.Id, Admin, new("ssh-push"), new(id, ExpectedRemoteRef.Absent), Token);
		Assert.Equal(1, await store.TryClaimProfiledWriteAsync(Context, push, "push", DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1), Admin, Token));
		var work = await store.ReadPushWorkAsync(Context, push, "push", 1, Admin, Token);
		Assert.Equal(receipt.Commit, work.Command.Commit);
		Assert.Equal(snapshot.CopyContent(), Assert.Single(work.LocalCommit.Command.Snapshots).CopyContent());
		var intent = JsonSerializer.SerializeToUtf8Bytes(new StoredRemotePush(1, fixture.Binding.Id, new(push, receipt.Commit, receipt.WorkBranch)));
		Assert.True(await store.SaveResultAsync(Context.TenantId, push, "push", 1, intent, DateTimeOffset.UtcNow, Token));
		await ExpireReadAsync(fixture.Db, push);
		Assert.Equal(2, await store.TryClaimProfiledWriteAsync(Context, push, "recovery", DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1), Admin, Token));
		Assert.Equal(SourceControlOperationState.Reconciling, (await store.ReadPushWorkAsync(Context, push, "recovery", 2, Admin, Token)).State);
		Assert.Equal(intent, await store.ReadSavedResultAsync(Context.TenantId, push, "recovery", 2, DateTimeOffset.UtcNow, Token));
		Assert.Equal(GitPushReconciliationResult.Confirmed, GitPushReconciliation.AfterUncertainWrite(work.Command.Commit, receipt.Commit));
	}
}
