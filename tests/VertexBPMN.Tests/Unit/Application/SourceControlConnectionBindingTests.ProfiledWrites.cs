using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using VertexBPMN.Infrastructure.SourceControl;
using VertexBPMN.Application.SourceControl;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Tests.Unit.Application;

public sealed partial class SourceControlConnectionBindingTests
{
	[Fact]
	public async Task Profiled_commit_is_versioned_claimed_and_reconstructed_without_a_github_credential()
	{
		await using var fixture = await Fixture.OpenAsync();
		var (store, id, submission) = await AcceptProfiledCommitAsync(fixture);
		Assert.Null(await store.TryClaimAsync(Context.TenantId, id, "legacy", DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1), Token));
		Assert.Equal(1, await store.TryClaimProfiledWriteAsync(Context, id, "profiled", DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1), Admin, Token));
		var work = await store.ReadCommitWorkAsync(Context, id, "profiled", 1, Admin, Token);
		Assert.Equal(submission.BaseCommit, work.Command.BaseCommit);
		Assert.Equal(submission.Snapshots[0].CopyContent(), Assert.Single(work.Command.Snapshots).CopyContent());
		Assert.Equal(fixture.Profile.Id, work.Binding.ConnectionProfileId);
		Assert.NotNull((await store.ReadClaimedWriteExecutionAsync(Context, id, "profiled", 1, Admin, Token)).WriteRequestDigest);
		await using var restarted = fixture.NewContext();
		Assert.Equal(work.Command.BaseCommit, (await fixture.NewStore(restarted, HttpsPreparationOptions(fixture))
			.ReadCommitWorkAsync(Context, id, "profiled", 1, Admin, Token)).Command.BaseCommit);
	}

	[Fact]
	public async Task Profiled_commit_retry_uses_the_same_operation_and_expired_claims_reconcile()
	{
		await using var fixture = await Fixture.OpenAsync();
		var (store, id, submission) = await AcceptProfiledCommitAsync(fixture);
		Assert.Equal(id, await store.EnqueueCommitAsync(Context, fixture.Binding.Id, Admin, new("profiled-commit"), submission, Token));
		Assert.Equal(1, await store.TryClaimProfiledWriteAsync(Context, id, "first", DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1), Admin, Token));
		await ExpireReadAsync(fixture.Db, id);
		Assert.Equal(2, await store.TryClaimProfiledWriteAsync(Context, id, "restart", DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1), Admin, Token));
		Assert.Equal(SourceControlOperationState.Reconciling, (await store.ReadCommitWorkAsync(Context, id, "restart", 2, Admin, Token)).State);
		Assert.False(await store.FinishAsync(Context.TenantId, id, "first", 1, SourceControlOperationState.CommittedLocal, DateTimeOffset.UtcNow, Token));
	}

	[Theory]
	[InlineData("role")]
	[InlineData("profile")]
	[InlineData("rotation")]
	public async Task Profiled_write_claim_rechecks_authority_before_incrementing_its_fence(string change)
	{
		await using var fixture = await Fixture.OpenAsync();
		var (store, id, _) = await AcceptProfiledCommitAsync(fixture);
		if (change == "profile")
		{
			Assert.True(await store.RevokeConnectionProfileAsync(Context, Admin, fixture.Profile.Id, 1, Token));
		}
		else if (change == "rotation")
		{
			Assert.True(await store.ReplaceConnectionProfileAsync(Context, Admin, 1,
				fixture.Profile with { Revision = 2, GitCredentialReference = "rotated-reference" }, Token));
		}
		await Assert.ThrowsAsync<SourceControlSecurityException>(() => store.TryClaimProfiledWriteAsync(Context, id, "denied",
			DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1), change == "role" ? [] : Admin, Token));
		Assert.Equal(0, (await fixture.Db.SourceControlOperations.AsNoTracking().SingleAsync(Token)).Fence);
	}

	[Theory]
	[InlineData("request")]
	[InlineData("execution")]
	public async Task Profiled_write_evidence_cannot_be_replayed_between_operations(string copied)
	{
		await using var fixture = await Fixture.OpenAsync();
		var (store, id, submission) = await AcceptProfiledCommitAsync(fixture);
		var other = await store.EnqueueCommitAsync(Context, fixture.Binding.Id, Admin, new("another-commit"), submission, Token);
		var otherRow = await fixture.Db.SourceControlOperations.AsNoTracking().SingleAsync(row => row.Id == other, Token);
		if (copied == "request")
		{
			await fixture.Db.SourceControlOperations.Where(row => row.Id == id)
				.ExecuteUpdateAsync(update => update.SetProperty(row => row.ProtectedRequest, otherRow.ProtectedRequest), Token);
		}
		else
		{
			await fixture.Db.SourceControlOperations.Where(row => row.Id == id)
				.ExecuteUpdateAsync(update => update.SetProperty(row => row.ProtectedExecutionTarget, otherRow.ProtectedExecutionTarget), Token);
		}
		await RejectAsync(SourceControlErrorCode.ContentUnsafe, () => store.TryClaimProfiledWriteAsync(Context, id, "replay",
			DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1), Admin, Token));
	}

	[Fact]
	public async Task Profiled_push_preserves_its_confirmed_source_bytes_and_expected_absent_ref()
	{
		await using var fixture = await Fixture.OpenAsync();
		var (store, id, submission) = await AcceptProfiledCommitAsync(fixture);
		Assert.Equal(1, await store.TryClaimProfiledWriteAsync(Context, id, "commit", DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1), Admin, Token));
		var snapshot = submission.Snapshots[0];
		var receipt = new CommitReceipt(id, submission.SessionId, new(new string('b', 40)), submission.WorkBranch,
			[new(snapshot.Path, snapshot.DocumentGeneration, snapshot.LocalRevision, snapshot.ContentSha256)]);
		Assert.True(await store.SaveResultAsync(Context.TenantId, id, "commit", 1,
			JsonSerializer.SerializeToUtf8Bytes(new StoredLocalCommit(1, fixture.Binding.Id, 1, receipt)), DateTimeOffset.UtcNow, Token));
		Assert.True(await store.FinishAsync(Context.TenantId, id, "commit", 1, SourceControlOperationState.CommittedLocal, DateTimeOffset.UtcNow, Token));
		var push = await store.EnqueuePushAsync(Context, fixture.Binding.Id, Admin, new("profiled-push"), new(id, ExpectedRemoteRef.Absent), Token);
		Assert.Equal(1, await store.TryClaimProfiledWriteAsync(Context, push, "push", DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1), Admin, Token));
		var work = await store.ReadPushWorkAsync(Context, push, "push", 1, Admin, Token);
		Assert.Equal(receipt.Commit, work.Command.Commit);
		Assert.Null(work.Command.ExpectedRemote.Commit);
		Assert.Equal(snapshot.CopyContent(), Assert.Single(work.LocalCommit.Command.Snapshots).CopyContent());
		Assert.Equal(SourceControlCapability.Push, (await store.ReadClaimedWriteExecutionAsync(Context, push, "push", 1, Admin, Token)).Target.RequiredCapabilities);
	}

	private static async Task<(PersistentSourceControlStore Store, Guid Id, CommitJobSubmission Submission)> AcceptProfiledCommitAsync(Fixture fixture,
		GitAuthenticationKind authentication = GitAuthenticationKind.HttpBasic)
	{
		if (authentication == GitAuthenticationKind.HttpBearer)
		{
			await AssignBearerAsync(fixture);
		}
		else
		{
			Assert.True(await fixture.AssignAsync());
		}
		var store = fixture.NewStore(fixture.Db, HttpsPreparationOptions(fixture));
		var generation = Guid.NewGuid();
		var session = await store.CreateSessionAsync(Context, fixture.Binding.Id, Admin, ReadRevision, generation, Token);
		var snapshot = new ModelSnapshot("models/process.bpmn", SourceModelKind.Bpmn, generation, 1,
			Encoding.UTF8.GetBytes("<definitions xmlns=\"http://www.omg.org/spec/BPMN/20100524/MODEL\"/>") );
		Assert.True(await store.SaveSnapshotsAsync(Context, session, Admin, 0, [snapshot], Token));
		var submission = new CommitJobSubmission(session, 1, ReadRevision, SourceControlInputPolicy.WorkBranch(session), "Confirmed", [snapshot]);
		return (store, await store.EnqueueCommitAsync(Context, fixture.Binding.Id, Admin, new("profiled-commit"), submission, Token), submission);
	}
}
