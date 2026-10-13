using System.Text;
using System.Text.Json;
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
	public async Task Hosting_jobs_are_protected_separately_and_cannot_be_claimed_by_Git_or_legacy_workers()
	{
		await using var fixture = await Fixture.OpenAsync();
		var setup = await HostedPushAsync(fixture);
		var id = await AcceptHostingAsync(fixture, setup);
		Assert.Equal(id, await AcceptHostingAsync(fixture, setup));
		var now = DateTimeOffset.UtcNow;
		Assert.Null(await setup.Store.TryClaimAsync(Context.TenantId, id, "legacy", now, TimeSpan.FromMinutes(1), Token));
		Assert.Null(await setup.Store.TryClaimProfiledWriteAsync(Context, id, "git", now, TimeSpan.FromMinutes(1), Admin, Token));
		Assert.Equal(1, await setup.Store.TryClaimHostingAsync(Context, id, "hosting", now, TimeSpan.FromMinutes(1), Admin, Token));
		var work = await setup.Store.ReadPullRequestWorkAsync(Context, id, "hosting", 1, Admin, Token);
		Assert.NotNull(work.Hosting);
		Assert.Equal("https://api.github.com/repos/team/repository", work.Hosting.HostingTarget.ApiRepositoryUri.AbsoluteUri);
		Assert.Equal("api-credential", work.Hosting.Target.Profile.Hosting!.CredentialReference);
		Assert.Equal(3, (await fixture.Db.SourceControlOperations.AsNoTracking().SingleAsync(row => row.Id == id, Token)).ExecutionContractVersion);
	}

	[Theory]
	[InlineData("request")]
	[InlineData("execution")]
	public async Task Hosting_evidence_cannot_be_replayed_from_another_operation(string copied)
	{
		await using var fixture = await Fixture.OpenAsync();
		var setup = await HostedPushAsync(fixture);
		var id = await AcceptHostingAsync(fixture, setup);
		var other = await setup.Store.EnqueuePullRequestAsync(Context, fixture.Binding.Id, Admin, new("other-hosting"),
			setup.Submission with { Title = "Other" }, Token);
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
		await RejectAsync(SourceControlErrorCode.ContentUnsafe, () => setup.Store.TryClaimHostingAsync(Context, id, "replay",
			DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1), Admin, Token));
	}

	[Theory]
	[InlineData('x')]
	[InlineData('\u00e9')]
	public async Task Maximum_valid_hosting_description_survives_protection_and_claiming(char content)
	{
		await using var fixture = await Fixture.OpenAsync();
		var setup = await HostedPushAsync(fixture);
		var description = new string(content, 16_384);
		var accepted = setup with { Submission = setup.Submission with { Description = description } };
		var id = await AcceptHostingAsync(fixture, accepted);
		Assert.Equal(1, await setup.Store.TryClaimHostingAsync(Context, id, "long-description", DateTimeOffset.UtcNow,
			TimeSpan.FromMinutes(1), Admin, Token));
		var work = await setup.Store.ReadPullRequestWorkAsync(Context, id, "long-description", 1, Admin, Token);
		Assert.Equal(description, work.Command.Description);
	}

	[Theory]
	[InlineData("role")]
	[InlineData("profile")]
	[InlineData("network")]
	public async Task Changed_authority_or_target_blocks_hosting_claim_before_any_external_call(string change)
	{
		await using var fixture = await Fixture.OpenAsync();
		var setup = await HostedPushAsync(fixture);
		var id = await AcceptHostingAsync(fixture, setup);
		if (change == "profile")
		{
			Assert.True(await setup.Store.ReplaceConnectionProfileAsync(Context, Admin, 2,
				setup.Profile with { Revision = 3, Hosting = setup.Profile.Hosting! with { CredentialReference = "rotated-api" } }, Token));
		}
		else if (change == "network")
		{
			setup.Monitor.SetupGet(value => value.CurrentValue).Returns(new SourceControlOptions
			{
				Enabled = true,
				HttpsNetworkPolicies = setup.Configuration.HttpsNetworkPolicies.Select(policy => policy.Id == "api-network"
					? policy with { Revision = 2 } : policy).ToArray()
			});
		}
		await Assert.ThrowsAsync<SourceControlSecurityException>(() => setup.Store.TryClaimHostingAsync(Context, id, "changed",
			DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1), change == "role" ? [] : Admin, Token));
		Assert.Equal(0, (await fixture.Db.SourceControlOperations.AsNoTracking().SingleAsync(row => row.Id == id, Token)).Fence);
	}

	[Fact]
	public async Task Profiled_hosting_reconciles_after_expiry_without_a_second_create_and_keeps_historical_receipt()
	{
		await using var fixture = await Fixture.OpenAsync();
		var setup = await HostedPushAsync(fixture);
		var id = await AcceptHostingAsync(fixture, setup);
		Assert.Equal(1, await setup.Store.TryClaimHostingAsync(Context, id, "first", DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1), Admin, Token));
		var adapter = new HostedLostReplyAdapter();
		var options = Options.Create(setup.Configuration);
		var tokenBroker = new GitHubAppTokenBroker(new SourceControlCredentialResolver(setup.Store, new ProfileCredentials()), options);
		var executor = new SourceControlPullRequestExecutor(setup.Store, tokenBroker, options, new([adapter]));
		await RejectAsync(SourceControlErrorCode.ResultUnknown, () => executor.ExecuteAsync(Context, id, "first", 1, HostedRolesAsync, Token));
		await ExpireReadAsync(fixture.Db, id);
		Assert.Equal(2, await setup.Store.TryClaimHostingAsync(Context, id, "recovery", DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1), Admin, Token));
		var receipt = await executor.ExecuteAsync(Context, id, "recovery", 2, HostedRolesAsync, Token);
		Assert.Equal(1, adapter.Creates);
		Assert.Equal(1, adapter.Reconciliations);
		Assert.Equal(receipt, await setup.Store.GetPullRequestReceiptAsync(Context, id, Admin, Token));
		Assert.True(await setup.Store.RevokeConnectionProfileAsync(Context, Admin, setup.Profile.Id, 2, Token));
		Assert.Equal(receipt, await setup.Store.GetPullRequestReceiptAsync(Context, id, Admin, Token));
		await RejectAsync(SourceControlErrorCode.ProviderUnavailable, () => setup.Store.ReadConfirmedPullRequestAsync(Context, id, Admin, Token));
	}

	private static Task<IReadOnlyCollection<string>> HostedRolesAsync(CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		return Task.FromResult<IReadOnlyCollection<string>>(Admin);
	}

	private static Task<Guid> AcceptHostingAsync(Fixture fixture, HostedSetup setup) =>
		setup.Store.EnqueuePullRequestAsync(Context, fixture.Binding.Id, Admin, new("profiled-hosting"), setup.Submission, Token);

	private static async Task<HostedSetup> HostedPushAsync(Fixture fixture, RepositoryHostingConnection? hosting = null)
	{
		var profile = fixture.Profile with
		{
			Revision = 2,
			Hosting = hosting ?? new("github", new(new Uri("https://api.github.com/repos/team/repository"), "api-network"),
				"team/repository", "api-credential", SourceControlCapability.PullRequest)
		};
		Assert.True(await fixture.Store.ReplaceConnectionProfileAsync(Context, Admin, 1, profile, Token));
		Assert.True(await fixture.Store.AssignConnectionProfileAsync(Context, Admin, fixture.Binding.Id, 1, profile.Id, 2, Token));
		Assert.True(await fixture.Store.ReplaceGrantsAsync(Context, fixture.Binding.Id, Admin, 2,
			[new(Context.ActorId, RepositoryPermission.Read | RepositoryPermission.Manage | RepositoryPermission.Commit
				| RepositoryPermission.Push | RepositoryPermission.PullRequest)], Token));
		var configuration = new SourceControlOptions
		{
			Enabled = true,
			HttpsNetworkPolicies = [new("repository-network", Context.TenantId, fixture.Binding.Remote, [], []),
				new("api-network", Context.TenantId, profile.Hosting.ApiEndpoint.BaseUri, [], [])]
		};
		var monitor = new Mock<IOptionsMonitor<SourceControlOptions>>();
		monitor.SetupGet(value => value.CurrentValue).Returns(configuration);
		var store = fixture.NewStore(fixture.Db, configuration, monitor.Object);
		var push = await ConfirmHostedPushAsync(fixture, store);
		return new(store, new(push, "main", "Hosted", "body"), profile, configuration, monitor);
	}

	private static async Task<Guid> ConfirmHostedPushAsync(Fixture fixture, PersistentSourceControlStore store)
	{
		var generation = Guid.NewGuid();
		var session = await store.CreateSessionAsync(Context, fixture.Binding.Id, Admin, ReadRevision, generation, Token);
		var snapshot = new ModelSnapshot("models/process.bpmn", SourceModelKind.Bpmn, generation, 1,
			Encoding.UTF8.GetBytes("<definitions xmlns=\"http://www.omg.org/spec/BPMN/20100524/MODEL\"/>"));
		Assert.True(await store.SaveSnapshotsAsync(Context, session, Admin, 0, [snapshot], Token));
		var submission = new CommitJobSubmission(session, 1, ReadRevision, SourceControlInputPolicy.WorkBranch(session), "Hosted", [snapshot]);
		var commit = await store.EnqueueCommitAsync(Context, fixture.Binding.Id, Admin, new("hosted-commit"), submission, Token);
		Assert.Equal(1, await store.TryClaimProfiledWriteAsync(Context, commit, "commit", DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1), Admin, Token));
		var receipt = new CommitReceipt(commit, session, new(new string('b', 40)), submission.WorkBranch,
			[new(snapshot.Path, snapshot.DocumentGeneration, snapshot.LocalRevision, snapshot.ContentSha256)]);
		Assert.True(await store.SaveResultAsync(Context.TenantId, commit, "commit", 1,
			JsonSerializer.SerializeToUtf8Bytes(new StoredLocalCommit(1, fixture.Binding.Id, 1, receipt)), DateTimeOffset.UtcNow, Token));
		Assert.True(await store.FinishAsync(Context.TenantId, commit, "commit", 1, SourceControlOperationState.CommittedLocal, DateTimeOffset.UtcNow, Token));
		var push = await store.EnqueuePushAsync(Context, fixture.Binding.Id, Admin, new("hosted-push"), new(commit, ExpectedRemoteRef.Absent), Token);
		Assert.Equal(1, await store.TryClaimProfiledWriteAsync(Context, push, "push", DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1), Admin, Token));
		Assert.True(await store.SaveResultAsync(Context.TenantId, push, "push", 1,
			JsonSerializer.SerializeToUtf8Bytes(new StoredRemotePush(1, fixture.Binding.Id, new(push, receipt.Commit, receipt.WorkBranch))), DateTimeOffset.UtcNow, Token));
		Assert.True(await store.FinishAsync(Context.TenantId, push, "push", 1, SourceControlOperationState.Pushed, DateTimeOffset.UtcNow, Token));
		return push;
	}

	private sealed record HostedSetup(PersistentSourceControlStore Store, PullRequestJobSubmission Submission,
		RepositoryConnectionProfile Profile, SourceControlOptions Configuration, Mock<IOptionsMonitor<SourceControlOptions>> Monitor);

	private sealed class HostedLostReplyAdapter : IProfiledRepositoryHostingAdapter
	{
		public string ProviderId => "github";
		public int Creates { get; private set; }
		public int Reconciliations { get; private set; }
		public Task<IRepositoryHostingSession> OpenAsync(SourceControlContext context, RepositoryBinding binding,
			IReadOnlyCollection<string> roles, RepositoryPermission permission, CancellationToken cancellationToken) => throw new NotSupportedException();
		public Task<IRepositoryHostingSession> OpenProfiledAsync(SourceControlContext context, RepositoryBinding binding,
			AcceptedHostingExecution execution, IReadOnlyCollection<string> roles, CancellationToken cancellationToken)
		{
			Assert.Equal(binding.Id, execution.Target.RepositoryId);
			cancellationToken.ThrowIfCancellationRequested();
			return Task.FromResult<IRepositoryHostingSession>(new HostedLostReplySession(this, execution.HostingTarget));
		}
		private sealed class HostedLostReplySession(HostedLostReplyAdapter owner, RepositoryHostingTarget target) : IRepositoryHostingSession
		{
			public Task<PullRequestReceipt> CreateAsync(PullRequestCommand command, CancellationToken cancellationToken)
			{
				cancellationToken.ThrowIfCancellationRequested();
				owner.Creates++;
				throw new SourceControlSecurityException(SourceControlErrorCode.ResultUnknown);
			}
			public Task<PullRequestReceipt> ReconcileAsync(PullRequestCommand command, CancellationToken cancellationToken)
			{
				cancellationToken.ThrowIfCancellationRequested();
				owner.Reconciliations++;
				return Task.FromResult(new PullRequestReceipt(command.OperationId, target.ProviderId, 17,
					new(target.WebRepositoryUri.AbsoluteUri + "/pull/17"), PullRequestState.Open, command.HeadCommit, null));
			}
			public Task<PullRequestReceipt> ReadAsync(PullRequestCommand command, long number, CancellationToken cancellationToken) => throw new NotSupportedException();
			public Task<IReadOnlyList<PullRequestReview>> ReadReviewsAsync(long number, CancellationToken cancellationToken) => throw new NotSupportedException();
			public void Dispose() { }
		}
	}
}
