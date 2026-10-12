using Microsoft.Extensions.Options;
using VertexBPMN.Application.SourceControl;
using VertexBPMN.Infrastructure.SourceControl;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Tests.Unit.Infrastructure;

public sealed partial class SourceControlPersistenceTests
{
	[Fact]
	public async Task Active_registry_path_reconciles_a_lost_create_without_a_second_write()
	{
		await using var fixture = new StoreFixture();
		var binding = await fixture.InitializeAsync();
		await using var db = fixture.Db();
		var store = fixture.Store(db);
		var (_, submission, _) = await ConfirmPushWithPullRequestGrantAsync(store, binding.Id);
		var id = await store.EnqueuePullRequestAsync(Actor, binding.Id, ["Admin"], new("registry-create"), submission, Cancellation);
		var first = await store.TryClaimAsync(Actor.TenantId, id, "pr-worker", DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1), Cancellation);
		Assert.NotNull(first);
		var adapter = new LostReplyAdapter();
		var configuration = Options.Create(new SourceControlOptions { Enabled = true });
		var broker = new GitHubAppTokenBroker(new SourceControlCredentialResolver(store, fixture.Credentials(db)), configuration);
		var executor = new SourceControlPullRequestExecutor(store, broker, configuration, new RepositoryHostingRegistry([adapter]));
		var error = await Assert.ThrowsAsync<SourceControlSecurityException>(() => executor.ExecuteAsync(Actor, id, "pr-worker",
			first.Value, ResolveRoles, Cancellation));
		Assert.Equal(SourceControlErrorCode.ResultUnknown, error.Code);
		var recovery = await AssertUnknownPullRequestRecoveryAsync(store, id, first.Value);
		var receipt = await executor.ExecuteAsync(Actor, id, "pr-recovery", recovery, ResolveRoles, Cancellation);
		Assert.Equal(receipt, await store.GetPullRequestReceiptAsync(Actor, id, ["Admin"], Cancellation));
		Assert.Equal(1, adapter.Creates);
		Assert.Equal(1, adapter.Reconciliations);
		Assert.Equal(2, adapter.Disposals);
	}

	private sealed class LostReplyAdapter : IRepositoryHostingAdapter
	{
		public string ProviderId => "github";
		internal int Creates { get; private set; }
		internal int Reconciliations { get; private set; }
		internal int Disposals { get; private set; }
		public Task<IRepositoryHostingSession> OpenAsync(SourceControlContext context, RepositoryBinding binding,
			IReadOnlyCollection<string> roles, RepositoryPermission permission, CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();
			Assert.Equal(Actor, context);
			Assert.Equal(RepositoryPermission.PullRequest, permission);
			Assert.Contains("Admin", roles, StringComparer.Ordinal);
			return Task.FromResult<IRepositoryHostingSession>(new LostReplySession(this));
		}

		private sealed class LostReplySession(LostReplyAdapter owner) : IRepositoryHostingSession
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
				return Task.FromResult(new PullRequestReceipt(command.OperationId, "github", 7,
					new("https://github.com/example/models/pull/7"), PullRequestState.Open, command.HeadCommit, null));
			}
			public Task<PullRequestReceipt> ReadAsync(PullRequestCommand command, long number, CancellationToken cancellationToken) => throw new NotSupportedException();
			public Task<IReadOnlyList<PullRequestReview>> ReadReviewsAsync(long number, CancellationToken cancellationToken) => throw new NotSupportedException();
			public void Dispose() => owner.Disposals++;
		}
	}
}
