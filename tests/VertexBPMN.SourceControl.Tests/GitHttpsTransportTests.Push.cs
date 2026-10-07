using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using VertexBPMN.Application.SourceControl;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Infrastructure.Persistence;
using VertexBPMN.Infrastructure.SourceControl;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.SourceControl.Tests;

public sealed partial class GitHttpsTransportTests
{
	private static CancellationToken PushCancellation => TestContext.Current.CancellationToken;

	[Theory]
	[InlineData("sha1", 40)]
	[InlineData("sha256", 64)]
	public async Task Https_push_creates_only_workbranch_and_independent_clone_reads_exact_bytes(string objectFormat, int objectIdLength)
	{
		await using var fixture = await HttpsFixture.CreateAsync(objectFormat: objectFormat);
		using var lease = new GitHubTokenLease(fixture.Token, DateTimeOffset.UtcNow.AddMinutes(3));
		await fixture.Runner.FetchLocalAcceptanceAsync(fixture.Workspace, fixture.Remote, "master", lease, fixture.CaFile, PushCancellation);
		var basis = await fixture.RemoteHeadAsync("master");
		var binding = new RepositoryBinding(Guid.NewGuid(), "test", fixture.Remote, null, "master", "release", ["models"]);
		var session = Guid.NewGuid();
		var bytes = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(fixture.ModelBytes).Replace("exact", "pushed", StringComparison.Ordinal));
		var local = new CommitCommand(Guid.NewGuid(), new("native-push"), session, basis, SourceControlInputPolicy.WorkBranch(session),
			"Push confirmed bytes", [new("models/example.bpmn", SourceModelKind.Bpmn, Guid.NewGuid(), 1, bytes)]);
		var target = await fixture.Runner.BuildCommitAsync(fixture.Workspace, binding, local, DateTimeOffset.UtcNow, PushCancellation);
		Assert.Equal(objectIdLength, basis.Value.Length);
		Assert.Equal(objectIdLength, target.Value.Length);
		var command = new PushCommand(Guid.NewGuid(), new("push-once"), local.WorkBranch, target, ExpectedRemoteRef.Absent);
		await fixture.Runner.PushLocalAcceptanceAsync(fixture.Workspace, binding, command, lease, fixture.CaFile, PushCancellation);
		Assert.Equal(target, await fixture.RemoteHeadAsync(command.WorkBranch));
		Assert.Equal(basis, await fixture.RemoteHeadAsync("master"));
		var clone = Path.Combine(fixture.Root, "independent-reader.git");
		await fixture.GitAsync(["clone", "--bare", "--no-local", Path.Combine(fixture.Root, "remote", "models.git"), clone]);
		Assert.Equal(bytes, await fixture.GitAsync(["--git-dir=" + clone, "cat-file", "blob", target.Value + ":models/example.bpmn"]));
		var conflict = await Assert.ThrowsAsync<SourceControlSecurityException>(() =>
			fixture.Runner.PushLocalAcceptanceAsync(fixture.Workspace, binding, command, lease, fixture.CaFile, PushCancellation));
		Assert.Equal(SourceControlErrorCode.RevisionConflict, conflict.Code);
		// Exercise a permitted fast-forward and reject rewinding the same branch.
		await fixture.SetRemoteHeadAsync(command.WorkBranch, basis);
		var forward = command with { ExpectedRemote = ExpectedRemoteRef.At(basis) };
		await fixture.Runner.PushLocalAcceptanceAsync(fixture.Workspace, binding, forward,
			lease, fixture.CaFile, PushCancellation);
		Assert.Equal(target, await fixture.RemoteHeadAsync(command.WorkBranch));
		var rewind = await Assert.ThrowsAsync<SourceControlSecurityException>(() => fixture.Runner.PushLocalAcceptanceAsync(fixture.Workspace,
			binding, command with { Commit = basis, ExpectedRemote = ExpectedRemoteRef.At(target) }, lease, fixture.CaFile, PushCancellation));
		Assert.Equal(SourceControlErrorCode.RevisionConflict, rewind.Code);
		Assert.Equal(target, await fixture.RemoteHeadAsync(command.WorkBranch));
		foreach (var branch in new[] { "master", "release", "other", "vertex/not-a-session" })
		{
			var forbidden = await Assert.ThrowsAsync<SourceControlSecurityException>(() => fixture.Runner.PushLocalAcceptanceAsync(
				fixture.Workspace, binding, command with { WorkBranch = branch }, lease, fixture.CaFile, PushCancellation));
			Assert.Equal(SourceControlErrorCode.InvalidInput, forbidden.Code);
		}
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task Https_push_server_CAS_rejects_reset_or_competing_commit_after_advertisement(bool reset)
	{
		await using var fixture = await HttpsFixture.CreateAsync();
		var original = await fixture.RemoteHeadAsync("master");
		var expected = await fixture.AddRemoteModelRevisionAsync("expected");
		using var lease = new GitHubTokenLease(fixture.Token, DateTimeOffset.UtcNow.AddMinutes(3));
		await fixture.Runner.FetchLocalAcceptanceAsync(fixture.Workspace, fixture.Remote, "master", lease, fixture.CaFile, PushCancellation);
		var binding = new RepositoryBinding(Guid.NewGuid(), "test", fixture.Remote, null, "master", "release", ["models"]);
		var session = Guid.NewGuid();
		var branch = SourceControlInputPolicy.WorkBranch(session);
		await fixture.SetRemoteHeadAsync(branch, expected);
		var local = new CommitCommand(Guid.NewGuid(), new("cas-commit"), session, expected, branch, "CAS child",
			[new("models/example.bpmn", SourceModelKind.Bpmn, Guid.NewGuid(), 1, fixture.ModelBytes)]);
		var target = await fixture.Runner.BuildCommitAsync(fixture.Workspace, binding, local, DateTimeOffset.UtcNow, PushCancellation);
		var concurrent = reset ? original : await fixture.AddRemoteModelRevisionAsync("concurrent");
		fixture.BeforeReceivePack = () => fixture.SetRemoteHeadAsync(branch, concurrent);
		var error = await Assert.ThrowsAsync<SourceControlSecurityException>(() => fixture.Runner.PushLocalAcceptanceAsync(fixture.Workspace,
			binding, new(Guid.NewGuid(), new("cas-push"), branch, target, ExpectedRemoteRef.At(expected)), lease, fixture.CaFile, PushCancellation));
		Assert.True(error.Code == SourceControlErrorCode.RevisionConflict, $"{error.Code}; rejection: {fixture.ReceiveRejection}; request lengths: {string.Join(',', fixture.ReceiveBodyLengths)}");
		Assert.Equal(1, fixture.PushRequests);
		Assert.Equal(concurrent, await fixture.RemoteHeadAsync(branch));
	}

	[Fact]
	public async Task Durable_https_push_lost_response_is_confirmed_without_resending()
	{
		await using var fixture = await HttpsFixture.CreateAsync();
		await using var durable = await DurablePushFixture.CreateAsync(fixture);
		fixture.DropPushResponse = true;
		var receipt = await durable.ExecuteAsync();
		Assert.Equal(durable.Commit.Commit, receipt.Commit);
		Assert.Equal(1, fixture.PushRequests);
		Assert.Equal(receipt.Commit, await fixture.RemoteHeadAsync(receipt.WorkBranch));
		await using var db = durable.Db();
		Assert.Equal(SourceControlOperationState.Pushed,
			(await durable.Store(db).GetOperationAsync(durable.Actor, durable.Id, ["Admin"], PushCancellation))!.State);
	}

	[Fact]
	public async Task Durable_https_push_DB_finish_failure_recovers_in_new_scope_without_another_push()
	{
		await using var fixture = await HttpsFixture.CreateAsync();
		await using var durable = await DurablePushFixture.CreateAsync(fixture);
		await using (var db = durable.Db())
		{
			await db.Database.ExecuteSqlRawAsync($"CREATE TRIGGER fail_push_finish BEFORE UPDATE OF State ON SourceControlOperations WHEN NEW.State = {(int)SourceControlOperationState.Pushed} BEGIN SELECT RAISE(ABORT, 'isolated finish failure'); END;", PushCancellation);
		}
		await Assert.ThrowsAnyAsync<Exception>(() => durable.ExecuteAsync());
		Assert.Equal(1, fixture.PushRequests);
		Assert.Equal(durable.Commit.Commit, await fixture.RemoteHeadAsync(durable.Commit.WorkBranch));
		await using (var db = durable.Db())
		{
			await db.Database.ExecuteSqlRawAsync("DROP TRIGGER fail_push_finish", PushCancellation);
			await db.SourceControlOperations.Where(x => x.Id == durable.Id)
				.ExecuteUpdateAsync(update => update.SetProperty(x => x.LeaseUntilUtcTicks, DateTimeOffset.UtcNow.AddSeconds(-1).UtcTicks), PushCancellation);
			Assert.Equal(1, await durable.Store(db).DetectExpiredLeasesAsync(DateTimeOffset.UtcNow, PushCancellation));
			Assert.Equal(3L, await durable.Store(db).TryClaimAsync(durable.Actor.TenantId, durable.Id, "recovery", DateTimeOffset.UtcNow,
				TimeSpan.FromMinutes(5), PushCancellation));
		}
		var receipt = await durable.ExecuteAsync("recovery", 3);
		Assert.Equal(durable.Commit.Commit, receipt.Commit);
		Assert.Equal(1, fixture.PushRequests);
	}

	[Fact]
	public async Task Durable_push_role_revocation_after_intent_blocks_remote_write()
	{
		await using var fixture = await HttpsFixture.CreateAsync();
		await using var durable = await DurablePushFixture.CreateAsync(fixture);
		var calls = 0;
		Task<IReadOnlyCollection<string>> Resolve(CancellationToken _) => Task.FromResult<IReadOnlyCollection<string>>(
			Interlocked.Increment(ref calls) == 1 ? ["Admin"] : ["ReadOnly"]);
		var error = await Assert.ThrowsAsync<SourceControlSecurityException>(() => durable.ExecuteAsync(resolve: Resolve));
		Assert.Equal(SourceControlErrorCode.Forbidden, error.Code);
		Assert.Equal(0, fixture.PushRequests);
		await using var db = durable.Db();
		Assert.NotNull(await durable.Store(db).ReadSavedResultAsync(durable.Actor.TenantId, durable.Id, "push", 1, DateTimeOffset.UtcNow, PushCancellation));
	}

	[Fact]
	public async Task Durable_push_acceptance_is_owned_idempotent_and_retains_unpublished_commit_after_pruning()
	{
		await using var fixture = await HttpsFixture.CreateAsync();
		await using var durable = await DurablePushFixture.CreateAsync(fixture);
		await using var db = durable.Db();
		var store = durable.Store(db);
		Assert.Equal(durable.Id, await store.EnqueuePushAsync(durable.Actor, durable.Binding.Id, ["Admin"], new("durable-push"),
			new(durable.Commit.OperationId, ExpectedRemoteRef.Absent), PushCancellation));
		Assert.Equal(SourceControlErrorCode.IdempotencyConflict, (await Assert.ThrowsAsync<SourceControlSecurityException>(() =>
			store.EnqueuePushAsync(durable.Actor, durable.Binding.Id, ["Admin"], new("durable-push"),
				new(durable.Commit.OperationId, ExpectedRemoteRef.At(durable.Commit.Commit)), PushCancellation))).Code);
		Assert.Equal(SourceControlErrorCode.NotFound, (await Assert.ThrowsAsync<SourceControlSecurityException>(() =>
			store.EnqueuePushAsync(new(durable.Actor.TenantId, "other"), durable.Binding.Id, ["Admin"], new("other-actor"),
				new(durable.Commit.OperationId, ExpectedRemoteRef.Absent), PushCancellation))).Code);
		await store.PruneExpiredDetailsAsync(DateTimeOffset.UtcNow.AddDays(60), PushCancellation);
		Assert.NotEmpty((await db.SourceControlOperations.AsNoTracking().SingleAsync(x => x.Id == durable.Commit.OperationId, PushCancellation)).ProtectedRequest);
		Assert.NotEqual(Guid.Empty, await store.EnqueuePushAsync(durable.Actor, durable.Binding.Id, ["Admin"], new("after-retention"),
			new(durable.Commit.OperationId, ExpectedRemoteRef.Absent), PushCancellation));
	}

	[Fact]
	public async Task Claimed_push_cancel_after_server_commit_is_unknown_and_recovers_without_resend()
	{
		await using var fixture = await HttpsFixture.CreateAsync();
		await using var durable = await DurablePushFixture.CreateAsync(fixture);
		var committed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var releaseResponse = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		fixture.AfterReceivePack = async token =>
		{
			committed.TrySetResult();
			await releaseResponse.Task.WaitAsync(token);
		};
		using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(PushCancellation);
		var execution = durable.RunClaimedAsync(cancellationToken: cancellation.Token);
		try
		{
			await committed.Task.WaitAsync(TimeSpan.FromSeconds(30), PushCancellation);
			Assert.Equal(durable.Commit.Commit, await fixture.RemoteHeadAsync(durable.Commit.WorkBranch));
			await cancellation.CancelAsync();
			var outcome = await execution.WaitAsync(TimeSpan.FromSeconds(20), PushCancellation);
			Assert.Equal(SourceControlOperationState.ResultUnknown, outcome.State);
			Assert.Null(outcome.Receipt);
			Assert.Equal(1, fixture.PushRequests);
		}
		finally
		{
			releaseResponse.TrySetResult();
			await cancellation.CancelAsync();
		}
		fixture.AfterReceivePack = null;
		await using (var db = durable.Db())
		{
			Assert.Equal(2L, await durable.Store(db).TryClaimAsync(durable.Actor.TenantId, durable.Id, "recovered", DateTimeOffset.UtcNow,
				TimeSpan.FromMinutes(5), PushCancellation));
		}
		var recovered = await durable.RunClaimedAsync("recovered", 2);
		Assert.Equal(SourceControlOperationState.Pushed, recovered.State);
		Assert.Equal(durable.Commit.Commit, recovered.Receipt!.Commit);
		Assert.Equal(1, fixture.PushRequests);
	}

	[Fact]
	public async Task Claimed_push_uncertain_intent_and_absent_head_never_replays_write()
	{
		await using var fixture = await HttpsFixture.CreateAsync();
		await using var durable = await DurablePushFixture.CreateAsync(fixture);
		await using (var db = durable.Db())
		{
			var store = durable.Store(db);
			Assert.True(await store.SaveResultAsync(durable.Actor.TenantId, durable.Id, "push", 1,
				System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(new StoredRemotePush(1, durable.Binding.Id,
					new(durable.Id, durable.Commit.Commit, durable.Commit.WorkBranch))), DateTimeOffset.UtcNow, PushCancellation));
			await db.SourceControlOperations.Where(x => x.Id == durable.Id).ExecuteUpdateAsync(update =>
				update.SetProperty(x => x.LeaseUntilUtcTicks, DateTimeOffset.UtcNow.AddSeconds(-1).UtcTicks), PushCancellation);
			Assert.Equal(1, await store.DetectExpiredLeasesAsync(DateTimeOffset.UtcNow, PushCancellation));
			Assert.Equal(3L, await store.TryClaimAsync(durable.Actor.TenantId, durable.Id, "uncertain", DateTimeOffset.UtcNow,
				TimeSpan.FromMinutes(5), PushCancellation));
		}
		var outcome = await durable.RunClaimedAsync("uncertain", 3);
		Assert.Equal(SourceControlOperationState.ResultUnknown, outcome.State);
		Assert.Equal(0, fixture.PushRequests);
		Assert.Null(outcome.Receipt);
	}

	[Fact]
	public async Task Claimed_push_concurrent_creation_is_conflict_and_preserves_other_head()
	{
		await using var fixture = await HttpsFixture.CreateAsync();
		await using var durable = await DurablePushFixture.CreateAsync(fixture);
		var concurrent = await fixture.RemoteHeadAsync("master");
		fixture.BeforeReceivePack = () => fixture.SetRemoteHeadAsync(durable.Commit.WorkBranch, concurrent);
		var outcome = await durable.RunClaimedAsync();
		Assert.True(outcome.State == SourceControlOperationState.Conflict, $"{outcome.State}; error: {outcome.Error}; rejection: {fixture.ReceiveRejection}");
		Assert.Equal(SourceControlErrorCode.RevisionConflict, outcome.Error);
		Assert.Equal(concurrent, await fixture.RemoteHeadAsync(durable.Commit.WorkBranch));
		Assert.Equal(1, fixture.PushRequests);
	}

	private sealed class DurablePushFixture(HttpsFixture transport) : IAsyncDisposable
	{
		private readonly IDataProtectionProvider _protection = new EphemeralDataProtectionProvider();
		private readonly IOptions<SourceControlOptions> _options = Options.Create(new SourceControlOptions
		{
			Enabled = true,
			AllowedHosts = ["github.com"],
			GitExecutablePath = Environment.GetEnvironmentVariable("VERTEXBPMN_TEST_GIT"),
			WorkspaceRoot = Path.Combine(transport.Root, "owned-push-workspaces")
		});
		internal SourceControlContext Actor { get; } = new("push-tenant", "issuer|push-actor");
		internal RepositoryBinding Binding { get; private set; } = null!;
		internal CommitReceipt Commit { get; private set; } = null!;
		internal Guid Id { get; private set; }
		internal BpmnDbContext Db() => new(new DbContextOptionsBuilder<BpmnDbContext>()
			.UseSqlite($"Data Source={Path.Combine(transport.Root, "push.db")};Pooling=False").Options);
		internal PersistentSourceControlStore Store(BpmnDbContext db) => new(db, _protection, _options);
		private SourceControlWorkspace Workspaces(BpmnDbContext db) => new(db, _protection, _options);
		private GitHubAppTokenBroker Broker(PersistentSourceControlStore store) =>
			new(new SourceControlCredentialResolver(store, new PushNoRemoteCredentials()), _options);

		internal static async Task<DurablePushFixture> CreateAsync(HttpsFixture transport)
		{
			var fixture = new DurablePushFixture(transport);
			await fixture.InitializeAsync();
			return fixture;
		}

		private async Task InitializeAsync()
		{
			await using var db = Db();
			await db.Database.MigrateAsync(PushCancellation);
			var store = Store(db);
			Binding = new(Guid.NewGuid(), Actor.TenantId, new("https://github.com/example/isolated.git"), null, "master", "release", ["models"]);
			await store.CreateBindingAsync(Actor, ["Admin"], Binding, PushCancellation);
			Assert.True(await store.ReplaceGrantsAsync(Actor, Binding.Id, ["Admin"], 1,
				[new(Actor.ActorId, RepositoryPermission.Read | RepositoryPermission.Manage | RepositoryPermission.Commit | RepositoryPermission.Push)], PushCancellation));
			var basis = await transport.RemoteHeadAsync("master");
			var generation = Guid.NewGuid();
			var session = await store.CreateSessionAsync(Actor, Binding.Id, ["Admin"], basis, generation, PushCancellation);
			var snapshot = new ModelSnapshot("models/example.bpmn", SourceModelKind.Bpmn, generation, 1,
				Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(transport.ModelBytes).Replace("exact", "durable", StringComparison.Ordinal)));
			Assert.True(await store.SaveSnapshotsAsync(Actor, session, ["Admin"], 0, [snapshot], PushCancellation));
			var localId = await store.EnqueueCommitAsync(Actor, Binding.Id, ["Admin"], new("durable-local"),
				new(session, 1, basis, SourceControlInputPolicy.WorkBranch(session), "Durable push source", [snapshot]), PushCancellation);
			Assert.Equal(1L, await store.TryClaimAsync(Actor.TenantId, localId, "commit", DateTimeOffset.UtcNow, TimeSpan.FromMinutes(5), PushCancellation));
			var workspace = await Workspaces(db).CreateAsync(Actor, localId, "commit", 1, PushCancellation);
			await transport.Runner.InitializeAsync(workspace, PushCancellation);
			using var lease = new GitHubTokenLease(transport.Token, DateTimeOffset.UtcNow.AddMinutes(3));
			await transport.Runner.FetchLocalAcceptanceAsync(workspace, transport.Remote, "master", lease, transport.CaFile, PushCancellation, basis);
			Commit = await new SourceControlCommitExecutor(store, Workspaces(db), transport.Runner, Broker(store))
				.ExecutePreparedAsync(Actor, localId, "commit", 1, ["Admin"], workspace, PushCancellation);
			Id = await store.EnqueuePushAsync(Actor, Binding.Id, ["Admin"], new("durable-push"),
				new(localId, ExpectedRemoteRef.Absent), PushCancellation);
			Assert.Equal(1L, await store.TryClaimAsync(Actor.TenantId, Id, "push", DateTimeOffset.UtcNow, TimeSpan.FromMinutes(5), PushCancellation));
		}

		internal async Task<PushReceipt> ExecuteAsync(string worker = "push", long fence = 1,
			Func<CancellationToken, Task<IReadOnlyCollection<string>>>? resolve = null,
			CancellationToken cancellationToken = default, SourceControlPushExecutor? executor = null)
		{
			if (cancellationToken == default)
			{
				cancellationToken = PushCancellation;
			}
			await using var db = Db();
			var store = Store(db);
			var workspaces = Workspaces(db);
			var workspace = await workspaces.CreateAsync(Actor, Id, worker, fence, cancellationToken);
			await transport.Runner.InitializeAsync(workspace, cancellationToken);
			using var lease = new GitHubTokenLease(transport.Token, DateTimeOffset.UtcNow.AddMinutes(3));
			if (fence == 1)
			{
				var work = await store.ReadPushWorkAsync(Actor, Id, worker, fence, ["Admin"], cancellationToken);
				await transport.Runner.FetchLocalAcceptanceAsync(workspace, transport.Remote, "master", lease, transport.CaFile, cancellationToken, work.LocalCommit.Command.BaseCommit);
			}
			var binding = Binding with { Remote = transport.Remote };
			return await (executor ?? new SourceControlPushExecutor(store, workspaces, transport.Runner, Broker(store))).ExecutePreparedAsync(
				Actor, Id, worker, fence, resolve ?? (_ => Task.FromResult<IReadOnlyCollection<string>>(["Admin"])), workspace,
				(command, token) => transport.Runner.ReadPushHeadLocalAcceptanceAsync(workspace, binding, command, lease, transport.CaFile, token),
				(command, token) => transport.Runner.PushLocalAcceptanceAsync(workspace, binding, command, lease, transport.CaFile, token), cancellationToken);
		}

		internal async Task<ClaimedPushOutcome> RunClaimedAsync(string worker = "push", long fence = 1,
			CancellationToken cancellationToken = default)
		{
			if (cancellationToken == default)
			{
				cancellationToken = PushCancellation;
			}
			await using var services = new ServiceCollection().AddScoped(_ => Db())
				.AddScoped(sp => Store(sp.GetRequiredService<BpmnDbContext>()))
				.AddScoped(sp => new SourceControlPushExecutor(sp.GetRequiredService<PersistentSourceControlStore>(),
					Workspaces(sp.GetRequiredService<BpmnDbContext>()), transport.Runner, Broker(sp.GetRequiredService<PersistentSourceControlStore>())))
				.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
			return await new SourceControlClaimedPushRunner(services.GetRequiredService<IServiceScopeFactory>()).RunPreparedAsync(
				Actor, Id, worker, fence, _ => Task.FromResult<IReadOnlyCollection<string>>(["Admin"]),
				(executor, token) => ExecuteAsync(worker, fence, cancellationToken: token, executor: executor), TimeSpan.FromSeconds(30), cancellationToken);
		}

		// The enclosing HTTPS fixture owns this isolated root, including the DB.
		public ValueTask DisposeAsync() => ValueTask.CompletedTask;
	}

	private sealed class PushNoRemoteCredentials : ICredentialService
	{
		public Task<IReadOnlyList<CredentialMetadata>> ListAsync(string t, CancellationToken c = default) => throw new InvalidOperationException();
		public Task<CredentialMetadata?> GetAsync(string t, string i, CancellationToken c = default) => throw new InvalidOperationException();
		public Task<CredentialMetadata> CreateAsync(string t, CredentialWriteRequest r, CancellationToken c = default) => throw new InvalidOperationException();
		public Task<bool> UpdateMetadataAsync(string t, string i, CredentialMetadataUpdate r, CancellationToken c = default) => throw new InvalidOperationException();
		public Task<bool> RotateSecretAsync(string t, string i, CredentialSecretRotation r, CancellationToken c = default) => throw new InvalidOperationException();
		public Task<bool> DeleteAsync(string t, string i, CancellationToken c = default) => throw new InvalidOperationException();
		public Task<string?> ResolveSecretAsync(string t, string i, string k, CancellationToken c = default) => throw new InvalidOperationException();
	}
}
