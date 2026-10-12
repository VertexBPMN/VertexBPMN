using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using VertexBPMN.Application.SourceControl;
using VertexBPMN.Infrastructure.Persistence;
using VertexBPMN.Infrastructure.SourceControl;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Tests.Unit.Application;

public sealed partial class SourceControlConnectionBindingTests
{
	[Fact]
	public async Task Read_runner_returns_value_only_after_durable_finish_and_removes_its_workspace()
	{
		await using var fixture = await Fixture.OpenAsync();
		await using var harness = await ReadRunnerHarness.OpenAsync(fixture);
		var outcome = await harness.RunAsync((_, _) => Task.CompletedTask, Token);
		Assert.Equal(SourceControlOperationState.Succeeded, outcome.State);
		Assert.Equal(ReadRevision.Value, outcome.Value);
		Assert.Null(outcome.Error);
		Assert.False(outcome.CleanupPending);
		Assert.False(Directory.Exists(harness.WorkspacePath));
		var row = await fixture.Db.SourceControlOperations.AsNoTracking().SingleAsync(Token);
		Assert.Equal((int)SourceControlOperationState.Succeeded, row.State);
		Assert.Null(row.LeaseOwner);
		Assert.True(harness.ContextIds.Distinct().Count() >= 4);
	}

	[Fact]
	public async Task Read_runner_scrubs_unexpected_failure_and_finalizes_in_a_fresh_scope()
	{
		await using var fixture = await Fixture.OpenAsync();
		await using var harness = await ReadRunnerHarness.OpenAsync(fixture);
		var outcome = await harness.RunAsync((_, _) => throw new InvalidOperationException("backend-password-must-not-escape"), Token);
		Assert.Equal(SourceControlOperationState.Failed, outcome.State);
		Assert.Equal(SourceControlErrorCode.ProviderUnavailable, outcome.Error);
		Assert.Null(outcome.Value);
		Assert.False(outcome.CleanupPending);
		Assert.False(Directory.Exists(harness.WorkspacePath));
		Assert.Equal((int)SourceControlOperationState.Failed,
			(await fixture.Db.SourceControlOperations.AsNoTracking().SingleAsync(Token)).State);
	}

	[Fact]
	public async Task Read_runner_finishes_caller_cancellation_with_an_independent_token()
	{
		await using var fixture = await Fixture.OpenAsync();
		await using var harness = await ReadRunnerHarness.OpenAsync(fixture);
		using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Token);
		var outcome = await harness.RunAsync(async (_, token) =>
		{
			await cancellation.CancelAsync();
			token.ThrowIfCancellationRequested();
		}, cancellation.Token);
		Assert.Equal(SourceControlOperationState.Cancelled, outcome.State);
		Assert.Equal(SourceControlErrorCode.Cancelled, outcome.Error);
		Assert.Null(outcome.Value);
		Assert.False(outcome.CleanupPending);
		Assert.False(Directory.Exists(harness.WorkspacePath));
	}

	[Theory]
	[InlineData("fetch")]
	[InlineData("projection")]
	public async Task Read_runner_bounds_fetch_and_projection_by_the_read_deadline(string stage)
	{
		await using var fixture = await Fixture.OpenAsync();
		await using var harness = await ReadRunnerHarness.OpenAsync(fixture, TimeSpan.FromSeconds(2));
		async Task<string> ReadAsync(GitWorkspace workspace, AcceptedGitRevisionReadWork work, CancellationToken token)
		{
			Assert.Equal(harness.WorkspacePath, workspace.Directory);
			if (stage == "projection")
			{
				await Task.Delay(Timeout.InfiniteTimeSpan, token);
			}
			return work.FixedRevision.Value;
		}
		var outcome = await harness.Runner.RunControlledAsync(Context, harness.Id, "runner", 1, (executor, token) =>
			executor.ExecuteControlledAsync(Context, harness.Id, "runner", 1,
				(_, fetchToken) => stage == "fetch" ? Task.Delay(Timeout.InfiniteTimeSpan, fetchToken) : Task.CompletedTask,
				ReadAsync, token), TimeSpan.FromSeconds(30), Token);
		Assert.Equal(SourceControlOperationState.Failed, outcome.State);
		Assert.Equal(SourceControlErrorCode.TimedOut, outcome.Error);
		Assert.Null(outcome.Value);
		Assert.False(outcome.CleanupPending);
	}

	[Fact]
	public async Task Read_runner_cannot_finish_or_clean_up_a_replacement_workers_active_claim()
	{
		await using var fixture = await Fixture.OpenAsync();
		await using var harness = await ReadRunnerHarness.OpenAsync(fixture);
		var outcome = await harness.RunAsync(async (_, token) =>
		{
			await fixture.Db.SourceControlOperations.Where(row => row.Id == harness.Id).ExecuteUpdateAsync(update => update
				.SetProperty(row => row.Fence, 2).SetProperty(row => row.LeaseOwner, "replacement")
				.SetProperty(row => row.LeaseUntilUtcTicks, DateTimeOffset.UtcNow.AddMinutes(2).UtcTicks), token);
		}, Token);
		Assert.Equal(SourceControlOperationState.ResultUnknown, outcome.State);
		Assert.Null(outcome.Value);
		Assert.True(outcome.CleanupPending);
		Assert.True(Directory.Exists(harness.WorkspacePath));
		var row = await fixture.Db.SourceControlOperations.AsNoTracking().SingleAsync(Token);
		Assert.Equal(2, row.Fence);
		Assert.Equal("replacement", row.LeaseOwner);
		Assert.Equal((int)SourceControlOperationState.Running, row.State);
	}

	[Fact]
	public async Task Read_runner_preserves_a_successful_outcome_when_workspace_ownership_is_tampered()
	{
		await using var fixture = await Fixture.OpenAsync();
		await using var harness = await ReadRunnerHarness.OpenAsync(fixture);
		var outcome = await harness.RunAsync((workspace, token) =>
			File.WriteAllTextAsync(Path.Combine(workspace.Directory, ".vertex-owner"), "foreign-marker", token), Token);
		Assert.Equal(SourceControlOperationState.Succeeded, outcome.State);
		Assert.Equal(ReadRevision.Value, outcome.Value);
		Assert.True(outcome.CleanupPending);
		Assert.Equal("foreign-marker", await File.ReadAllTextAsync(Path.Combine(harness.WorkspacePath, ".vertex-owner"), Token));
	}

	[Fact]
	public async Task Read_runner_rechecks_authority_after_fetch_before_exposing_any_value()
	{
		await using var fixture = await Fixture.OpenAsync();
		await using var harness = await ReadRunnerHarness.OpenAsync(fixture);
		var outcome = await harness.RunAsync((_, _) =>
		{
			harness.Actors.Roles = [];
			return Task.CompletedTask;
		}, Token);
		Assert.Equal(SourceControlOperationState.Failed, outcome.State);
		Assert.Null(outcome.Value);
		Assert.False(outcome.CleanupPending);
	}

	[Fact]
	public async Task Read_runner_heartbeat_cancels_work_when_fresh_authority_is_revoked()
	{
		await using var fixture = await Fixture.OpenAsync();
		await using var harness = await ReadRunnerHarness.OpenAsync(fixture);
		var cancellationObserved = false;
		var outcome = await harness.RunAsync(async (_, token) =>
		{
			harness.Actors.Roles = [];
			try
			{
				await Task.Delay(Timeout.InfiniteTimeSpan, token);
			}
			catch (OperationCanceledException)
			{
				cancellationObserved = true;
				throw;
			}
		}, TimeSpan.FromMilliseconds(50), Token);
		Assert.True(cancellationObserved);
		Assert.Equal(SourceControlOperationState.ResultUnknown, outcome.State);
		Assert.Equal(SourceControlErrorCode.ResultUnknown, outcome.Error);
		Assert.Null(outcome.Value);
		Assert.False(outcome.CleanupPending);
	}

	[Fact]
	public async Task Read_runner_finalization_rechecks_authority_even_when_a_callback_returns_a_value()
	{
		await using var fixture = await Fixture.OpenAsync();
		await using var harness = await ReadRunnerHarness.OpenAsync(fixture);
		var outcome = await harness.Runner.RunControlledAsync(Context, harness.Id, "runner", 1, (_, _) =>
		{
			harness.Actors.Roles = [];
			return Task.FromResult("must-not-be-delivered");
		}, TimeSpan.FromSeconds(30), Token);
		Assert.Equal(SourceControlOperationState.Failed, outcome.State);
		Assert.Null(outcome.Value);
		Assert.Equal((int)SourceControlOperationState.Failed,
			(await fixture.Db.SourceControlOperations.AsNoTracking().SingleAsync(Token)).State);
	}

	[Fact]
	public async Task Worker_shutdown_preserves_a_recoverable_read_instead_of_a_terminal_cancellation()
	{
		await using var fixture = await Fixture.OpenAsync();
		await using var harness = await ReadRunnerHarness.OpenAsync(fixture);
		using var shutdown = CancellationTokenSource.CreateLinkedTokenSource(Token);
		var outcome = await harness.Runner.RunControlledWorkerAsync<string>(Context, harness.Id, "runner", 1, async (_, token) =>
		{
			await shutdown.CancelAsync();
			token.ThrowIfCancellationRequested();
			return "must-not-be-delivered";
		}, TimeSpan.FromSeconds(30), shutdown.Token);
		Assert.Equal(SourceControlOperationState.ResultUnknown, outcome.State);
		Assert.Null(outcome.Value);
		var registry = ExecutionRegistry(() => HttpsPreparationOptions(fixture));
		Assert.Equal(2, await fixture.Store.TryClaimHttpsRevisionReadAsync(Context, harness.Id, "restart", DateTimeOffset.UtcNow,
			TimeSpan.FromMinutes(1), Admin, registry, Token));
	}

	[Fact]
	public async Task Read_runner_rejects_foreign_actor_before_workspace_creation_or_state_change()
	{
		await using var fixture = await Fixture.OpenAsync();
		await using var harness = await ReadRunnerHarness.OpenAsync(fixture);
		await RejectAsync(SourceControlErrorCode.NotFound, () => harness.Runner.RunControlledAsync<string>(
			new(Context.TenantId, "foreign-actor"), harness.Id, "runner", 1, (_, _) => Task.FromResult("forbidden"),
			TimeSpan.FromSeconds(30), Token));
		Assert.False(Directory.Exists(harness.Root));
		Assert.Equal((int)SourceControlOperationState.Running,
			(await fixture.Db.SourceControlOperations.AsNoTracking().SingleAsync(Token)).State);
	}

	private sealed class ReadRunnerHarness : IAsyncDisposable
	{
		private readonly ServiceProvider services;
		public Guid Id { get; }
		public string Root { get; }
		public string WorkspacePath => Path.Combine(Root, $"{Id:N}-1");
		public List<Guid> ContextIds { get; } = [];
		public CredentialActors Actors { get; } = new();
		public SourceControlClaimedReadRunner Runner { get; }

		private ReadRunnerHarness(Fixture fixture, SourceControlOptions configuration, GitHttpsPolicyRegistry registry, Guid id)
		{
			Id = id;
			Root = configuration.WorkspaceRoot!;
			var options = Options.Create(configuration);
			var registrations = new ServiceCollection();
			registrations.AddScoped(_ =>
			{
				var db = fixture.NewContext();
				ContextIds.Add(db.ContextId.InstanceId);
				return db;
			});
			registrations.AddScoped(provider => fixture.NewStore(provider.GetRequiredService<BpmnDbContext>(), configuration));
			registrations.AddScoped(provider => fixture.NewWorkspaces(provider.GetRequiredService<BpmnDbContext>(), configuration));
			registrations.AddSingleton<ISourceControlActorResolver>(Actors);
			registrations.AddSingleton(registry);
			registrations.AddScoped(provider =>
			{
				var store = provider.GetRequiredService<PersistentSourceControlStore>();
				return new ProfiledGitHttpsTransport(store, Actors, registry,
					new(store, new ProfileCredentials(), Actors, options), new(options), options);
			});
			registrations.AddScoped(provider => new SourceControlHttpsReadExecutor(
				provider.GetRequiredService<PersistentSourceControlStore>(), provider.GetRequiredService<SourceControlWorkspace>(),
				provider.GetRequiredService<ProfiledGitHttpsTransport>(), Actors, registry));
			services = registrations.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
			Runner = new(services.GetRequiredService<IServiceScopeFactory>(), options);
		}

		public static async Task<ReadRunnerHarness> OpenAsync(Fixture fixture, TimeSpan? timeout = null)
		{
			Assert.True(await fixture.AssignAsync());
			var configuration = new SourceControlOptions
			{
				Enabled = true,
				WorkspaceRoot = Path.Combine(Path.GetTempPath(), "vertex-read-runner-" + Guid.NewGuid().ToString("N")),
				HttpsNetworkPolicies = HttpsPreparationOptions(fixture).HttpsNetworkPolicies,
				Limits = new() { ReadTimeout = timeout ?? TimeSpan.FromSeconds(15) }
			};
			var registry = ExecutionRegistry(() => configuration);
			var id = await AcceptRevisionReadAsync(fixture, registry);
			Assert.Equal(1, await fixture.Store.TryClaimHttpsRevisionReadAsync(Context, id, "runner", DateTimeOffset.UtcNow,
				TimeSpan.FromMinutes(1), Admin, registry, Token));
			return new(fixture, configuration, registry, id);
		}

		public Task<ClaimedReadOutcome<string>> RunAsync(Func<GitWorkspace, CancellationToken, Task> fetch,
			CancellationToken cancellationToken) => RunAsync(fetch, TimeSpan.FromSeconds(30), cancellationToken);

		public Task<ClaimedReadOutcome<string>> RunAsync(Func<GitWorkspace, CancellationToken, Task> fetch,
			TimeSpan interval, CancellationToken cancellationToken) =>
			Runner.RunControlledAsync(Context, Id, "runner", 1, (executor, token) =>
				executor.ExecuteControlledAsync(Context, Id, "runner", 1, fetch,
					(_, work, _) => Task.FromResult(work.FixedRevision.Value), token), interval, cancellationToken);

		public async ValueTask DisposeAsync()
		{
			await services.DisposeAsync();
			var resolved = Path.GetFullPath(Root);
			Assert.Equal(Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar), Path.GetDirectoryName(resolved));
			Assert.StartsWith("vertex-read-runner-", Path.GetFileName(resolved), StringComparison.Ordinal);
			if (Directory.Exists(resolved))
			{
				Directory.Delete(resolved, recursive: true);
			}
		}
	}
}
