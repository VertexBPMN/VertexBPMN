using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using VertexBPMN.Application.SourceControl;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Infrastructure.Persistence;
using VertexBPMN.Infrastructure.Persistence.Services;
using VertexBPMN.Infrastructure.SourceControl;
using VertexBPMN.SourceControl.Abstractions;
using ModelSnapshot = VertexBPMN.SourceControl.Abstractions.ModelSnapshot;

namespace VertexBPMN.Tests.Unit.Infrastructure;

public sealed class SourceControlPersistenceTests
{
    private static readonly SourceControlContext Actor = new("tenant-a", "issuer|alice");
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Pull_request_acceptance_requires_grant_and_confirmed_push_and_is_idempotent()
    {
        await using var fixture = new StoreFixture();
        var binding = await fixture.InitializeAsync();
        await using var db = fixture.Db();
        var store = fixture.Store(db);
        var push = await store.EnqueueAsync(Actor, binding.Id, ["Admin"], SourceControlOperationKind.Push,
            new("pr-source-push"), new byte[] { 1 }, Cancellation);
        var submission = new PullRequestJobSubmission(push, "master", "Review model", "Model update");
        await Assert.ThrowsAsync<SourceControlSecurityException>(() => store.EnqueuePullRequestAsync(
            Actor, binding.Id, ["Admin"], new("pr-create"), submission, Cancellation));
        Assert.True(await store.ReplaceGrantsAsync(Actor, binding.Id, ["Admin"], 2,
            [new(Actor.ActorId, RepositoryPermission.Read | RepositoryPermission.Manage | RepositoryPermission.Push
                | RepositoryPermission.PullRequest)], Cancellation));
        await Assert.ThrowsAsync<SourceControlSecurityException>(() => store.EnqueuePullRequestAsync(
            Actor, binding.Id, ["Admin"], new("pr-create"), submission, Cancellation));
        var now = DateTimeOffset.UtcNow;
        var fence = await store.TryClaimAsync(Actor.TenantId, push, "push-worker", now, TimeSpan.FromMinutes(1), Cancellation);
        Assert.NotNull(fence);
        var receipt = new PushReceipt(push, new(new string('a', 40)), "vertex/model-review");
        Assert.True(await store.SaveResultAsync(Actor.TenantId, push, "push-worker", fence.Value,
            System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(new StoredRemotePush(1, binding.Id, receipt)), now, Cancellation));
        Assert.True(await store.FinishAsync(Actor.TenantId, push, "push-worker", fence.Value,
            SourceControlOperationState.Pushed, now, Cancellation));
        var operation = await store.EnqueuePullRequestAsync(Actor, binding.Id, ["Admin"], new("pr-create"), submission, Cancellation);
        Assert.Equal(operation, await store.EnqueuePullRequestAsync(Actor, binding.Id, ["Admin"], new("pr-create"), submission, Cancellation));
        await Assert.ThrowsAsync<SourceControlSecurityException>(() => store.EnqueuePullRequestAsync(
            Actor, binding.Id, ["Admin"], new("pr-create"), submission with { Title = "Different review" }, Cancellation));
        Assert.Single(await db.SourceControlOperations.AsNoTracking().Where(x => x.Kind == (int)SourceControlOperationKind.PullRequest).ToArrayAsync(Cancellation));
        var prFence = await store.TryClaimAsync(Actor.TenantId, operation, "pr-worker", DateTimeOffset.UtcNow,
            TimeSpan.FromMinutes(1), Cancellation);
        Assert.NotNull(prFence);
        var work = await store.ReadPullRequestWorkAsync(Actor, operation, "pr-worker", prFence.Value, ["Admin"], Cancellation);
        Assert.Equal(receipt.Commit, work.Command.HeadCommit);
        Assert.Equal(receipt.WorkBranch, work.Command.WorkBranch);
        Assert.Equal(push, work.PushOperationId);
        var executor = new SourceControlPullRequestExecutor(store,
            new GitHubAppTokenBroker(new SourceControlCredentialResolver(store, fixture.Credentials(db)),
                Options.Create(new SourceControlOptions { Enabled = true })), Options.Create(new SourceControlOptions { Enabled = true }));
        var createCalls = 0;
        Task<IReadOnlyCollection<string>> ResolveRoles(CancellationToken token) => Task.FromResult<IReadOnlyCollection<string>>(["Admin"]);
        Task<PullRequestReceipt> LostCreate(AcceptedPullRequestWork current, CancellationToken token)
        {
            createCalls++;
            throw new SourceControlSecurityException(SourceControlErrorCode.ResultUnknown);
        }
        Task<PullRequestReceipt> UnexpectedReconcile(AcceptedPullRequestWork current, CancellationToken token) =>
            throw new InvalidOperationException("First attempt must create once.");
        await Assert.ThrowsAsync<SourceControlSecurityException>(() => executor.ExecutePreparedAsync(Actor, operation,
            "pr-worker", prFence.Value, ResolveRoles, LostCreate, UnexpectedReconcile, Cancellation));
        Assert.Equal(1, createCalls);
        Assert.False(await store.SavePullRequestIntentAsync(Actor, operation, "pr-worker", prFence.Value, ["Admin"], Cancellation));
        await Assert.ThrowsAsync<SourceControlSecurityException>(() => store.ReadPullRequestWorkAsync(
            Actor, operation, "pr-worker", prFence.Value + 1, ["Admin"], Cancellation));
        await Assert.ThrowsAsync<SourceControlSecurityException>(() => store.ReadPullRequestWorkAsync(
            new("tenant-b", Actor.ActorId), operation, "pr-worker", prFence.Value, ["Admin"], Cancellation));
        Assert.True(await store.FinishAsync(Actor.TenantId, operation, "pr-worker", prFence.Value,
            SourceControlOperationState.ResultUnknown, DateTimeOffset.UtcNow, Cancellation));
        var recoveryFence = await store.TryClaimAsync(Actor.TenantId, operation, "pr-recovery", DateTimeOffset.UtcNow,
            TimeSpan.FromMinutes(1), Cancellation);
        Assert.NotNull(recoveryFence);
        await Assert.ThrowsAsync<SourceControlSecurityException>(() => store.SavePullRequestIntentAsync(
            Actor, operation, "pr-recovery", recoveryFence.Value, ["Admin"], Cancellation));
        var prReceipt = new PullRequestReceipt(operation, "github", 7, new("https://github.com/example/models/pull/7"),
            PullRequestState.Open, receipt.Commit, null);
        var reconcileCalls = 0;
        Task<PullRequestReceipt> ConfirmExisting(AcceptedPullRequestWork current, CancellationToken token)
        {
            reconcileCalls++;
            return Task.FromResult(prReceipt);
        }
        var runnerServices = new ServiceCollection();
        runnerServices.AddScoped(_ => fixture.Db());
        runnerServices.AddScoped(sp => fixture.Store(sp.GetRequiredService<BpmnDbContext>()));
        runnerServices.AddScoped(sp => new SourceControlPullRequestExecutor(sp.GetRequiredService<PersistentSourceControlStore>(),
            new GitHubAppTokenBroker(new SourceControlCredentialResolver(sp.GetRequiredService<PersistentSourceControlStore>(),
                fixture.Credentials(sp.GetRequiredService<BpmnDbContext>())), Options.Create(new SourceControlOptions { Enabled = true })),
            Options.Create(new SourceControlOptions { Enabled = true })));
        await using var runnerProvider = runnerServices.BuildServiceProvider();
        var runner = new SourceControlClaimedPullRequestRunner(runnerProvider.GetRequiredService<IServiceScopeFactory>());
        var outcome = await runner.RunPreparedAsync(Actor, operation, "pr-recovery", recoveryFence.Value, ResolveRoles,
            (claimedExecutor, token) => claimedExecutor.ExecutePreparedAsync(Actor, operation, "pr-recovery", recoveryFence.Value,
                ResolveRoles, LostCreate, ConfirmExisting, token), Cancellation);
        Assert.Equal(SourceControlOperationState.Succeeded, outcome.State);
        Assert.Equal(prReceipt, outcome.Receipt);
        Assert.Equal(prReceipt, await store.GetPullRequestReceiptAsync(Actor, operation, ["Admin"], Cancellation));
        Assert.Null(await store.GetPullRequestReceiptAsync(new("tenant-b", Actor.ActorId), operation, ["Admin"], Cancellation));
        var confirmed = await store.ReadConfirmedPullRequestAsync(Actor, operation, ["Admin"], Cancellation);
        Assert.Equal(binding.Id, confirmed.Binding.Id);
        Assert.Equal(work.Command, confirmed.Command);
        Assert.Equal(prReceipt, confirmed.Receipt);
        await Assert.ThrowsAsync<SourceControlSecurityException>(() => store.ReadConfirmedPullRequestAsync(
            new("tenant-b", Actor.ActorId), operation, ["Admin"], Cancellation));
        await Assert.ThrowsAsync<SourceControlSecurityException>(() => store.ReadConfirmedPullRequestAsync(
            new(Actor.TenantId, "other-user"), operation, ["Admin"], Cancellation));
        await Assert.ThrowsAsync<SourceControlSecurityException>(() => store.ReadConfirmedPullRequestAsync(
            Actor, operation, ["ReadOnly"], Cancellation));
        Assert.Equal(1, createCalls);
        Assert.Equal(1, reconcileCalls);
        Assert.Equal(SourceControlOperationState.Succeeded,
            (await store.GetOperationAsync(Actor, operation, ["Admin"], Cancellation))!.State);
        var revokedOperation = await store.EnqueuePullRequestAsync(Actor, binding.Id, ["Admin"],
            new("pr-revocation"), submission, Cancellation);
        var revokedFence = await store.TryClaimAsync(Actor.TenantId, revokedOperation, "revoked-worker",
            DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1), Cancellation);
        Assert.NotNull(revokedFence);
        Assert.True(await store.ReplaceGrantsAsync(Actor, binding.Id, ["Admin"], 3,
            [new(Actor.ActorId, RepositoryPermission.Read | RepositoryPermission.Manage)], Cancellation));
        await Assert.ThrowsAsync<SourceControlSecurityException>(() => store.ReadPullRequestWorkAsync(
            Actor, revokedOperation, "revoked-worker", revokedFence.Value, ["Admin"], Cancellation));
        await Assert.ThrowsAsync<SourceControlSecurityException>(() => store.ReadConfirmedPullRequestAsync(
            Actor, operation, ["Admin"], Cancellation));
        var denied = await runner.RunPreparedAsync(Actor, revokedOperation, "revoked-worker", revokedFence.Value,
            ResolveRoles, (_, _) => throw new InvalidOperationException("Revoked job must not reach remote execution."), Cancellation);
        Assert.Equal(SourceControlOperationState.Failed, denied.State);
        Assert.Equal(1, createCalls);
    }

    [Fact]
    public async Task Typed_commit_acceptance_freezes_confirmed_bytes_and_retries_after_later_edits()
    {
        await using var fixture = new StoreFixture();
        var binding = await fixture.InitializeAsync();
        await using var db = fixture.Db();
        var store = fixture.Store(db);
        var generation = Guid.NewGuid();
        var basis = new GitCommitId(new string('a', 40));
        var session = await store.CreateSessionAsync(Actor, binding.Id, ["Admin"], basis, generation, Cancellation);
        var original = Encoding.UTF8.GetBytes("<definitions xmlns=\"http://www.omg.org/spec/BPMN/20100524/MODEL\">\r\n<process id=\"first\"/>\r\n</definitions>");
        var first = new ModelSnapshot("models/first.bpmn", SourceModelKind.Bpmn, generation, 1, original);
        var second = new ModelSnapshot("models/second.bpmn", SourceModelKind.Bpmn, generation, 1, original);
        Assert.True(await store.SaveSnapshotsAsync(Actor, session, ["Admin"], 0, [first, second], Cancellation));
        var submission = new CommitJobSubmission(session, 1, basis, SourceControlInputPolicy.WorkBranch(session), "Confirmed snapshot", [second, first]);
        var id = await store.EnqueueCommitAsync(Actor, binding.Id, ["Admin"], new("typed-commit"), submission, Cancellation);
        var next = new ModelSnapshot(first.Path, first.Kind, generation, 2,
            Encoding.UTF8.GetBytes("<definitions xmlns=\"http://www.omg.org/spec/BPMN/20100524/MODEL\"><process id=\"later\"/></definitions>"));
        Assert.True(await store.SaveSnapshotsAsync(Actor, session, ["Admin"], 1, [next, second], Cancellation));
        Assert.Equal(id, await store.EnqueueCommitAsync(Actor, binding.Id, ["Admin"], new("typed-commit"),
            submission with { Snapshots = [first, second] }, Cancellation)); // Order-independent canonical input.
        var conflict = await Assert.ThrowsAsync<SourceControlSecurityException>(() => store.EnqueueCommitAsync(Actor, binding.Id,
            ["Admin"], new("typed-commit"), submission with { Message = "Different request" }, Cancellation));
        Assert.Equal(SourceControlErrorCode.IdempotencyConflict, conflict.Code);
        var stale = await Assert.ThrowsAsync<SourceControlSecurityException>(() => store.EnqueueCommitAsync(Actor, binding.Id,
            ["Admin"], new("typed-stale"), submission, Cancellation));
        Assert.Equal(SourceControlErrorCode.RevisionConflict, stale.Code);
        var now = DateTimeOffset.UtcNow;
        Assert.Equal(1L, await store.TryClaimAsync(Actor.TenantId, id, "worker", now, TimeSpan.FromMinutes(1), Cancellation));
        using var accepted = System.Text.Json.JsonDocument.Parse(await store.ReadAcceptedRequestAsync(Actor.TenantId, id, "worker", 1, now, Cancellation));
        Assert.Equal(2, accepted.RootElement.GetProperty("SchemaVersion").GetInt32());
        Assert.True(accepted.RootElement.GetProperty("AcceptedUtcTicks").GetInt64() > 0);
        Assert.Equal(2, accepted.RootElement.GetProperty("BindingRevision").GetInt64());
        Assert.Equal(basis.Value, accepted.RootElement.GetProperty("BaseCommit").GetString());
        var acceptedFirst = accepted.RootElement.GetProperty("Snapshots")[0];
        Assert.Equal(first.Path, acceptedFirst.GetProperty("Path").GetString());
        Assert.Equal(original, acceptedFirst.GetProperty("Bytes").GetBytesFromBase64());
        Assert.Equal(first.ContentSha256, acceptedFirst.GetProperty("ContentSha256").GetString());
        Assert.Equal(1, acceptedFirst.GetProperty("Revision").GetInt64());
        Assert.Equal(2, (await store.ReadSnapshotsAsync(Actor, session, ["Admin"], Cancellation)).Single(x => x.Path == first.Path).LocalRevision);
        Assert.Single(await db.SourceControlOperations.AsNoTracking().ToArrayAsync(Cancellation));
    }

    [Fact]
    public async Task Typed_commit_acceptance_rejects_unconfirmed_content_branch_and_revoked_rights()
    {
        await using var fixture = new StoreFixture();
        var binding = await fixture.InitializeAsync();
        await using var db = fixture.Db();
        var store = fixture.Store(db);
        var generation = Guid.NewGuid();
        var basis = new GitCommitId(new string('a', 40));
        var session = await store.CreateSessionAsync(Actor, binding.Id, ["Admin"], basis, generation, Cancellation);
        var bytes = Encoding.UTF8.GetBytes("<definitions xmlns=\"http://www.omg.org/spec/BPMN/20100524/MODEL\"/>");
        var snapshot = new ModelSnapshot("models/safe.bpmn", SourceModelKind.Bpmn, generation, 1, bytes);
        Assert.True(await store.SaveSnapshotsAsync(Actor, session, ["Admin"], 0, [snapshot], Cancellation));
        var submission = new CommitJobSubmission(session, 1, basis, SourceControlInputPolicy.WorkBranch(session), "Commit", [snapshot]);
        var unconfirmed = new ModelSnapshot(snapshot.Path, snapshot.Kind, generation, 1,
            Encoding.UTF8.GetBytes("<definitions xmlns=\"http://www.omg.org/spec/BPMN/20100524/MODEL\"><process id=\"not-saved\"/></definitions>"));
        var error = await Assert.ThrowsAsync<SourceControlSecurityException>(() => store.EnqueueCommitAsync(Actor, binding.Id,
            ["Admin"], new("unconfirmed"), submission with { Snapshots = [unconfirmed] }, Cancellation));
        Assert.Equal(SourceControlErrorCode.RevisionConflict, error.Code);
        error = await Assert.ThrowsAsync<SourceControlSecurityException>(() => store.EnqueueCommitAsync(Actor, binding.Id,
            ["Admin"], new("default-branch"), submission with { WorkBranch = "master" }, Cancellation));
        Assert.Equal(SourceControlErrorCode.InvalidInput, error.Code);
        error = await Assert.ThrowsAsync<SourceControlSecurityException>(() => store.EnqueueCommitAsync(Actor, binding.Id,
            ["Admin"], new("duplicate-path"), submission with { Snapshots = [snapshot, snapshot] }, Cancellation));
        Assert.Equal(SourceControlErrorCode.InvalidInput, error.Code);
        Assert.True(await store.ReplaceGrantsAsync(Actor, binding.Id, ["Admin"], 2,
            [new(Actor.ActorId, RepositoryPermission.Read | RepositoryPermission.Manage)], Cancellation));
        error = await Assert.ThrowsAsync<SourceControlSecurityException>(() => store.EnqueueCommitAsync(Actor, binding.Id,
            ["Admin"], new("revoked"), submission, Cancellation));
        Assert.Equal(SourceControlErrorCode.Forbidden, error.Code);
        Assert.Empty(await db.SourceControlOperations.AsNoTracking().ToArrayAsync(Cancellation));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Real_host_startup_detects_crashed_jobs_only_when_integration_is_enabled(bool enabled)
    {
        await using var fixture = new StoreFixture();
        var binding = await fixture.InitializeAsync();
        Guid id;
        await using (var db = fixture.Db())
        {
            var store = fixture.Store(db);
            id = await store.EnqueueAsync(Actor, binding.Id, ["Admin"], SourceControlOperationKind.Push,
                new("host-startup"), new byte[] { 1 }, Cancellation);
            Assert.Equal(1L, await store.TryClaimAsync(Actor.TenantId, id, "crashed", DateTimeOffset.UtcNow.AddMinutes(-1),
                TimeSpan.FromSeconds(1), Cancellation));
        }
        var unusedRoot = Path.Combine(Path.GetTempPath(), "vertex-unused-maintenance-" + Guid.NewGuid().ToString("N"));
        using var host = Host.CreateDefaultBuilder().ConfigureLogging(logging => logging.ClearProviders())
            .ConfigureServices(services =>
            {
                services.AddSingleton(Options.Create(new SourceControlOptions { Enabled = enabled, WorkspaceRoot = unusedRoot }));
                services.AddScoped(_ => fixture.Db());
                services.AddScoped(sp => fixture.Store(sp.GetRequiredService<BpmnDbContext>()));
                services.AddScoped(sp => fixture.Workspace(sp.GetRequiredService<BpmnDbContext>(), unusedRoot));
                services.AddHostedService<SourceControlMaintenanceHostedService>();
            }).Build();
        await host.StartAsync(Cancellation);
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Cancellation);
            timeout.CancelAfter(TimeSpan.FromSeconds(10));
            if (!enabled)
            {
                var service = Assert.IsType<SourceControlMaintenanceHostedService>(Assert.Single(host.Services.GetServices<IHostedService>()));
                await service.ExecuteTask!.WaitAsync(timeout.Token);
            }
            while (true)
            {
                await using var db = fixture.Db();
                var row = await db.SourceControlOperations.AsNoTracking().SingleAsync(x => x.Id == id, timeout.Token);
                if (!enabled) { Assert.Equal((int)SourceControlOperationState.Running, row.State); break; }
                if (row.State == (int)SourceControlOperationState.ResultUnknown) { Assert.Equal(2L, row.Fence); break; }
                await Task.Delay(25, timeout.Token); // Condition polling, not an assumed completion sleep.
            }
            Assert.False(Directory.Exists(unusedRoot));
        }
        finally { await host.StopAsync(Cancellation); }
    }

    [Fact]
    public async Task Maintenance_fences_expired_workers_without_replaying_or_pruning_unknown_effects()
    {
        await using var fixture = new StoreFixture();
        var binding = await fixture.InitializeAsync();
        Guid expired, active, queued;
        var now = DateTimeOffset.UtcNow;
        await using (var db = fixture.Db())
        {
            var store = fixture.Store(db);
            expired = await store.EnqueueAsync(Actor, binding.Id, ["Admin"], SourceControlOperationKind.Push,
                new("expired-worker"), new byte[] { 1 }, Cancellation);
            active = await store.EnqueueAsync(Actor, binding.Id, ["Admin"], SourceControlOperationKind.Push,
                new("active-worker"), new byte[] { 2 }, Cancellation);
            queued = await store.EnqueueAsync(Actor, binding.Id, ["Admin"], SourceControlOperationKind.Push,
                new("queued-worker"), new byte[] { 3 }, Cancellation);
            Assert.Equal(1L, await store.TryClaimAsync(Actor.TenantId, expired, "old", now, TimeSpan.FromSeconds(1), Cancellation));
            Assert.Equal(1L, await store.TryClaimAsync(Actor.TenantId, active, "active", now, TimeSpan.FromMinutes(5), Cancellation));
        }
        // New DB scope emulates maintenance after the original request scope has ended.
        await using var reopened = fixture.Db();
        var maintenance = fixture.Store(reopened);
        Assert.Equal(1, await maintenance.DetectExpiredLeasesAsync(now.AddSeconds(2), Cancellation));
        Assert.Equal(0, await maintenance.DetectExpiredLeasesAsync(now.AddSeconds(2), Cancellation));
        var receipt = await reopened.SourceControlOperations.AsNoTracking().SingleAsync(x => x.Id == expired, Cancellation);
        Assert.Equal((int)SourceControlOperationState.ResultUnknown, receipt.State);
        Assert.Equal(2L, receipt.Fence);
        Assert.Null(receipt.LeaseOwner);
        Assert.NotEmpty(receipt.ProtectedRequest);
        Assert.Equal(SourceControlOperationState.Running, (await maintenance.GetOperationAsync(Actor, active, ["Admin"], Cancellation))!.State);
        Assert.Equal(SourceControlOperationState.Queued, (await maintenance.GetOperationAsync(Actor, queued, ["Admin"], Cancellation))!.State);
        Assert.False(await maintenance.FinishAsync(Actor.TenantId, expired, "old", 1, SourceControlOperationState.Pushed, now.AddSeconds(2), Cancellation));
        Assert.Equal(0, await maintenance.PruneExpiredDetailsAsync(now.AddDays(40), Cancellation));
        Assert.Equal(3L, await maintenance.TryClaimAsync(Actor.TenantId, expired, "reconciler", now.AddSeconds(2), TimeSpan.FromSeconds(10), Cancellation));
        Assert.Equal(SourceControlOperationState.Reconciling, (await maintenance.GetOperationAsync(Actor, expired, ["Admin"], Cancellation))!.State);
        Assert.Equal(new byte[] { 1 }, await maintenance.ReadAcceptedRequestAsync(Actor.TenantId, expired, "reconciler", 3, now.AddSeconds(3), Cancellation));
    }

    [Fact]
    public async Task Maintenance_cleans_only_expired_owned_terminal_workspaces_and_preserves_local_commits()
    {
        await using var fixture = new StoreFixture();
        var binding = await fixture.InitializeAsync();
        await using var db = fixture.Db();
        var store = fixture.Store(db);
        var root = Path.Combine(Path.GetTempPath(), "vertex-maintenance-" + Guid.NewGuid().ToString("N"));
        var workspace = fixture.Workspace(db, root);
        var now = DateTimeOffset.UtcNow;
        var retained = new List<string>();
        string? completedPath = null;
        try
        {
            foreach (var state in new[] { SourceControlOperationState.Pushed, SourceControlOperationState.CommittedLocal,
                SourceControlOperationState.ResultUnknown })
            {
                var kind = state == SourceControlOperationState.CommittedLocal ? SourceControlOperationKind.Commit : SourceControlOperationKind.Push;
                var id = await store.EnqueueAsync(Actor, binding.Id, ["Admin"], kind, new("cleanup-" + state), new byte[] { 1 }, Cancellation);
                Assert.Equal(1L, await store.TryClaimAsync(Actor.TenantId, id, "owner", now, TimeSpan.FromMinutes(1), Cancellation));
                var created = await workspace.CreateAsync(Actor, id, "owner", 1, Cancellation);
                await File.WriteAllTextAsync(Path.Combine(created.Directory, "fixture.txt"), "preserve", Cancellation);
                File.SetAttributes(Path.Combine(created.Directory, "fixture.txt"), FileAttributes.ReadOnly);
                Assert.True(await store.FinishAsync(Actor.TenantId, id, "owner", 1, state, now, Cancellation));
                if (state == SourceControlOperationState.Pushed) completedPath = created.Directory;
                else retained.Add(created.Directory);
            }
            var foreign = Path.Combine(root, "unknown-directory");
            Directory.CreateDirectory(foreign);
            retained.Add(foreign);
            Assert.Equal(0, await workspace.PruneCompletedAsync(now, Cancellation));
            Assert.Equal(1, await workspace.PruneCompletedAsync(now.AddDays(40), Cancellation));
            Assert.False(Directory.Exists(completedPath));
            Assert.All(retained, path => Assert.True(Directory.Exists(path)));
            Assert.Equal(0, await workspace.PruneCompletedAsync(now.AddDays(40), Cancellation));
            Assert.Equal(3, await db.SourceControlOperations.CountAsync(Cancellation)); // Durable receipts remain.
        }
        finally
        {
            if (Directory.Exists(root))
            {
                _ = SourceControlWorkspace.MeasureBytes(root, long.MaxValue);
                foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)) File.SetAttributes(file, FileAttributes.Normal);
                Directory.Delete(root, recursive: true); // Exact generated fixture, never user repositories.
            }
        }
    }

    [Fact]
    public async Task Retention_keeps_active_input_and_durable_idempotency_after_completion()
    {
        await using var fixture = new StoreFixture();
        var binding = await fixture.InitializeAsync();
        await using var db = fixture.Db();
        var store = fixture.Store(db);
        var request = new byte[] { 9 };
        var id = await store.EnqueueAsync(Actor, binding.Id, ["Admin"], SourceControlOperationKind.Push, new("retention"), request, Cancellation);
        var now = DateTimeOffset.UtcNow;
        Assert.Equal(0, await store.PruneExpiredDetailsAsync(now.AddDays(40), Cancellation));
        Assert.Equal(1L, await store.TryClaimAsync(Actor.TenantId, id, "owner", now, TimeSpan.FromMinutes(1), Cancellation));
        Assert.False(await store.FinishAsync(Actor.TenantId, id, "owner", 1, SourceControlOperationState.CommittedLocal, now, Cancellation));
        Assert.True(await store.FinishAsync(Actor.TenantId, id, "owner", 1, SourceControlOperationState.Pushed, now, Cancellation));
        Assert.Equal(1, await store.PruneExpiredDetailsAsync(now.AddDays(40), Cancellation));
        Assert.Equal("", (await db.SourceControlOperations.AsNoTracking().SingleAsync(Cancellation)).ProtectedRequest);
        Assert.Equal(id, await store.EnqueueAsync(Actor, binding.Id, ["Admin"], SourceControlOperationKind.Push, new("retention"), request, Cancellation));
    }

    [Fact]
    public async Task Push_envelope_has_bounded_room_for_the_original_commit_and_its_receipt()
    {
        await using var fixture = new StoreFixture();
        var binding = await fixture.InitializeAsync();
        await using var db = fixture.Db();
        var store = fixture.Store(db);
        var largerEnvelope = new byte[3 * 1024 * 1024 + 512];
        Assert.NotEqual(Guid.Empty, await store.EnqueueAsync(Actor, binding.Id, ["Admin"], SourceControlOperationKind.Push,
            new("push-envelope"), largerEnvelope, Cancellation));
        Assert.Equal(SourceControlErrorCode.PayloadTooLarge, (await Assert.ThrowsAsync<SourceControlSecurityException>(() =>
            store.EnqueueAsync(Actor, binding.Id, ["Admin"], SourceControlOperationKind.Commit,
                new("oversized-commit"), largerEnvelope, Cancellation))).Code);
        Assert.Equal(SourceControlErrorCode.PayloadTooLarge, (await Assert.ThrowsAsync<SourceControlSecurityException>(() =>
            store.EnqueueAsync(Actor, binding.Id, ["Admin"], SourceControlOperationKind.Push,
                new("oversized-push"), new byte[6 * 1024 * 1024 + 1], Cancellation))).Code);
    }

    [Fact]
    public async Task Global_job_quota_is_arbitrated_between_independent_workers()
    {
        await using var fixture = new StoreFixture();
        var binding = await fixture.InitializeAsync();
        var options = Options.Create(new SourceControlOptions { Enabled = true, AllowedHosts = ["github.com"],
            Limits = new() { MaxConcurrentJobsTotal = 1, MaxConcurrentJobsPerTenant = 1 } });
        Guid firstId, secondId;
        await using (var db = fixture.Db())
        {
            firstId = await fixture.Store(db).EnqueueAsync(Actor, binding.Id, ["Admin"], SourceControlOperationKind.Push, new("quota-one"), new byte[] { 1 }, Cancellation);
            secondId = await fixture.Store(db).EnqueueAsync(Actor, binding.Id, ["Admin"], SourceControlOperationKind.Push, new("quota-two"), new byte[] { 2 }, Cancellation);
        }
        await using var first = fixture.Db();
        await using var second = fixture.Db();
        var now = DateTimeOffset.UtcNow;
        var outcomes = await Task.WhenAll(new PersistentSourceControlStore(first, new EphemeralDataProtectionProvider(), options)
            .TryClaimAsync(Actor.TenantId, firstId, "one", now, TimeSpan.FromSeconds(10), Cancellation),
            new PersistentSourceControlStore(second, new EphemeralDataProtectionProvider(), options)
            .TryClaimAsync(Actor.TenantId, secondId, "two", now, TimeSpan.FromSeconds(10), Cancellation));
        Assert.Single(outcomes, x => x.HasValue);
    }

    [Fact]
    public async Task Workspace_reservation_enforces_quota_and_owner_checked_cleanup()
    {
        await using var fixture = new StoreFixture();
        var binding = await fixture.InitializeAsync();
        await using var db = fixture.Db();
        var store = fixture.Store(db);
        var first = await store.EnqueueAsync(Actor, binding.Id, ["Admin"], SourceControlOperationKind.Push, new("workspace-one"), new byte[] { 1 }, Cancellation);
        var second = await store.EnqueueAsync(Actor, binding.Id, ["Admin"], SourceControlOperationKind.Push, new("workspace-two"), new byte[] { 2 }, Cancellation);
        var now = DateTimeOffset.UtcNow;
        Assert.Equal(1L, await store.TryClaimAsync(Actor.TenantId, first, "worker", now, TimeSpan.FromMinutes(1), Cancellation));
        Assert.Equal(1L, await store.TryClaimAsync(Actor.TenantId, second, "worker", now, TimeSpan.FromMinutes(1), Cancellation));
        var root = Path.Combine(Path.GetTempPath(), "vertex-private-workspace-" + Guid.NewGuid().ToString("N"));
        var workspace = new SourceControlWorkspace(db, new EphemeralDataProtectionProvider(), Options.Create(new SourceControlOptions
        { Enabled = true, WorkspaceRoot = root, Limits = new() { MaxRepositoryBytes = 1024, MaxWorkspaceBytesTotal = 1600 } }));
        try
        {
            var created = await workspace.CreateAsync(Actor, first, "worker", 1, Cancellation);
            Assert.True(Directory.Exists(created.Directory));
            Assert.Equal(SourceControlErrorCode.QuotaExceeded, (await Assert.ThrowsAsync<SourceControlSecurityException>(() =>
                workspace.CreateAsync(Actor, second, "worker", 1, Cancellation))).Code);
            await Assert.ThrowsAsync<SourceControlSecurityException>(() => workspace.CleanupAsync(Actor, first, 1, Cancellation));
            Assert.True(await store.FinishAsync(Actor.TenantId, first, "worker", 1, SourceControlOperationState.Pushed, DateTimeOffset.UtcNow, Cancellation));
            await Assert.ThrowsAsync<SourceControlSecurityException>(() => workspace.CleanupAsync(new(Actor.TenantId, "foreign-actor"), first, 1, Cancellation));
            Assert.True(Directory.Exists(created.Directory));
            await workspace.CleanupAsync(Actor, first, 1, Cancellation);
            Assert.False(Directory.Exists(created.Directory));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                _ = SourceControlWorkspace.MeasureBytes(root, long.MaxValue);
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Lease_renewal_and_immutable_results_require_current_owner_and_fence()
    {
        await using var fixture = new StoreFixture();
        var binding = await fixture.InitializeAsync();
        await using var db = fixture.Db();
        var store = fixture.Store(db);
        var id = await store.EnqueueAsync(Actor, binding.Id, ["Admin"], SourceControlOperationKind.Push,
            new("recovery-result"), new byte[] { 7, 8 }, Cancellation);
        var now = DateTimeOffset.UtcNow;
        Assert.Equal(1L, await store.TryClaimAsync(Actor.TenantId, id, "owner", now, TimeSpan.FromSeconds(5), Cancellation));
        Assert.False(await store.RenewLeaseAsync(Actor.TenantId, id, "other", 1, now.AddSeconds(1), TimeSpan.FromSeconds(10), Cancellation));
        Assert.True(await store.RenewLeaseAsync(Actor.TenantId, id, "owner", 1, now.AddSeconds(1), TimeSpan.FromSeconds(10), Cancellation));
        Assert.Equal(new byte[] { 7, 8 }, await store.ReadAcceptedRequestAsync(Actor.TenantId, id, "owner", 1, now.AddSeconds(6), Cancellation));
        await Assert.ThrowsAsync<SourceControlSecurityException>(() => store.ReadAcceptedRequestAsync("foreign", id, "owner", 1, now, Cancellation));
        Assert.True(await store.SaveResultAsync(Actor.TenantId, id, "owner", 1, Encoding.UTF8.GetBytes("immutable-result"), now.AddSeconds(6), Cancellation));
        Assert.Equal(Encoding.UTF8.GetBytes("immutable-result"), await store.ReadSavedResultAsync(Actor.TenantId, id, "owner", 1, now.AddSeconds(6), Cancellation));
        Assert.False(await store.SaveResultAsync(Actor.TenantId, id, "owner", 1, new byte[] { 1 }, now.AddSeconds(6), Cancellation));
        Assert.DoesNotContain("immutable-result", (await db.SourceControlOperations.AsNoTracking().SingleAsync(Cancellation)).ProtectedResult!);
        Assert.False(await store.RenewLeaseAsync(Actor.TenantId, id, "owner", 1, now.AddSeconds(12), TimeSpan.FromSeconds(10), Cancellation));
    }

    [Fact]
    public async Task Invalid_grants_are_rejected_without_mutating_binding_revision()
    {
        await using var fixture = new StoreFixture();
        var binding = await fixture.InitializeAsync();
        await using var db = fixture.Db();
        var store = fixture.Store(db);
        foreach (var grants in new RepositoryGrant[][] {
            [new(Actor.ActorId, (RepositoryPermission)1024)],
            [new(Actor.ActorId, RepositoryPermission.Read), new(Actor.ActorId, RepositoryPermission.Push)],
            [new("actor\n", RepositoryPermission.Read)] })
            Assert.Equal(SourceControlErrorCode.InvalidInput, (await Assert.ThrowsAsync<SourceControlSecurityException>(() =>
                store.ReplaceGrantsAsync(Actor, binding.Id, ["Admin"], 2, grants, Cancellation))).Code);
        Assert.Equal(2, (await store.FindAsync(Actor.TenantId, binding.Id, Cancellation))!.Revision);
    }

    [Fact]
    public async Task Migration_upgrades_previous_schema_and_retains_existing_credentials()
    {
        await using var fixture = new StoreFixture();
        await using var db = fixture.Db();
        await db.GetService<IMigrator>().MigrateAsync("20260912090238_ExternalTaskPersistence", Cancellation);
        var credential = await fixture.Credentials(db).CreateAsync(Actor.TenantId,
            new("legacy", "GitHubApp", null, new Dictionary<string, string> { ["token"] = "test-value" }), Cancellation);
        await db.Database.MigrateAsync(Cancellation);
        Assert.False(db.Database.HasPendingModelChanges());
        Assert.Equal("test-value", await fixture.Credentials(db).ResolveSecretAsync(Actor.TenantId, credential.Id, "token", Cancellation));
        var store = fixture.Store(db);
        var binding = fixture.Binding(credential.Id);
        await store.CreateBindingAsync(Actor, ["Admin"], binding, Cancellation);
        Assert.NotNull(await store.FindAsync(Actor.TenantId, binding.Id, Cancellation));
        Assert.Null(await store.FindAsync("tenant-b", binding.Id, Cancellation));
    }

    [Fact]
    public async Task Idempotency_survives_new_context_and_rejects_different_input_or_actor()
    {
        await using var fixture = new StoreFixture();
        var binding = await fixture.InitializeAsync();
        Guid id;
        await using (var db = fixture.Db()) id = await fixture.Store(db).EnqueueAsync(Actor, binding.Id,
            ["Admin"], SourceControlOperationKind.Commit, new("same-key"), Encoding.UTF8.GetBytes("snapshot-one"), Cancellation);
        await using var reopened = fixture.Db();
        var store = fixture.Store(reopened);
        Assert.Equal(id, await store.EnqueueAsync(Actor, binding.Id, ["Admin"], SourceControlOperationKind.Commit,
            new("same-key"), Encoding.UTF8.GetBytes("snapshot-one"), Cancellation));
        Assert.Equal(SourceControlErrorCode.IdempotencyConflict, (await Assert.ThrowsAsync<SourceControlSecurityException>(() =>
            store.EnqueueAsync(Actor, binding.Id, ["Admin"], SourceControlOperationKind.Commit, new("same-key"),
                Encoding.UTF8.GetBytes("snapshot-two"), Cancellation))).Code);
        var row = await reopened.SourceControlOperations.AsNoTracking().SingleAsync(Cancellation);
        Assert.DoesNotContain("snapshot-one", row.ProtectedRequest);
        Assert.Single(await reopened.SourceControlOperations.ToListAsync(Cancellation));
    }

    [Fact]
    public async Task Parallel_claims_fence_stale_worker_and_require_reconciliation_after_expiry()
    {
        await using var fixture = new StoreFixture();
        var binding = await fixture.InitializeAsync();
        Guid id;
        await using (var db = fixture.Db()) id = await fixture.Store(db).EnqueueAsync(Actor, binding.Id,
            ["Admin"], SourceControlOperationKind.Push, new("claim-key"), new byte[] { 1 }, Cancellation);
        var now = DateTimeOffset.UtcNow;
        await using var firstDb = fixture.Db();
        await using var secondDb = fixture.Db();
        var results = await Task.WhenAll(fixture.Store(firstDb).TryClaimAsync(Actor.TenantId, id, "worker-a", now,
            TimeSpan.FromSeconds(5), Cancellation), fixture.Store(secondDb).TryClaimAsync(Actor.TenantId, id,
            "worker-b", now, TimeSpan.FromSeconds(5), Cancellation));
        Assert.Single(results.Where(x => x.HasValue));
        await using var restarted = fixture.Db();
        var store = fixture.Store(restarted);
        var fence = await store.TryClaimAsync(Actor.TenantId, id, "new-worker", now.AddSeconds(6), TimeSpan.FromSeconds(5), Cancellation);
        Assert.Equal(2L, fence);
        Assert.Equal(SourceControlOperationState.Reconciling,
            (await store.GetOperationAsync(Actor, id, ["Admin"], Cancellation))!.State);
        Assert.False(await store.FinishAsync(Actor.TenantId, id, "worker-a", 1, SourceControlOperationState.Pushed, now.AddSeconds(7), Cancellation));
        Assert.False(await store.FinishAsync(Actor.TenantId, id, "worker-b", 1, SourceControlOperationState.Pushed, now.AddSeconds(7), Cancellation));
        Assert.True(await store.FinishAsync(Actor.TenantId, id, "new-worker", 2, SourceControlOperationState.Pushed, now.AddSeconds(7), Cancellation));
    }

    [Fact]
    public async Task Session_snapshot_is_protected_byte_exact_owner_bound_and_cas_updated()
    {
        await using var fixture = new StoreFixture();
        var binding = await fixture.InitializeAsync();
        await using var db = fixture.Db();
        var store = fixture.Store(db);
        var generation = Guid.NewGuid();
        var session = await store.CreateSessionAsync(Actor, binding.Id, ["Admin"], new(new string('a', 40)), generation, Cancellation);
        var bytes = Encoding.UTF8.GetBytes("<definitions xmlns=\"http://www.omg.org/spec/BPMN/20100524/MODEL\">\r\n<process id=\"draft\"/></definitions>");
        var snapshot = new ModelSnapshot("models/draft.bpmn", SourceModelKind.Bpmn, generation, 1, bytes);
        Assert.True(await store.SaveSnapshotsAsync(Actor, session, ["Admin"], 0, [snapshot], Cancellation));
        Assert.False(await store.SaveSnapshotsAsync(Actor, session, ["Admin"], 0, [snapshot], Cancellation));
        var row = await db.SourceControlSessions.AsNoTracking().SingleAsync(Cancellation);
        Assert.DoesNotContain("definitions", row.ProtectedSnapshots);
        await using var reopened = fixture.Db();
        Assert.Equal(bytes, Assert.Single(await fixture.Store(reopened).ReadSnapshotsAsync(Actor, session, ["Admin"], Cancellation)).CopyContent());
        await Assert.ThrowsAsync<SourceControlSecurityException>(() => fixture.Store(reopened).ReadSnapshotsAsync(
            new(Actor.TenantId, "issuer|bob"), session, ["Admin"], Cancellation));
    }

    [Fact]
    public async Task Credential_resolution_uses_real_store_rotation_and_current_acl()
    {
        await using var fixture = new StoreFixture();
        await using var db = fixture.Db();
        await db.Database.MigrateAsync(Cancellation);
        var credentials = fixture.Credentials(db);
        var credential = await credentials.CreateAsync(Actor.TenantId, new("Git", "GitHubApp", null,
            new Dictionary<string, string> { ["privateKey"] = "key-before" }), Cancellation);
        var store = fixture.Store(db);
        var binding = fixture.Binding(credential.Id);
        await store.CreateBindingAsync(Actor, ["Admin"], binding, Cancellation);
        var grants = new RepositoryGrant[] { new(Actor.ActorId, RepositoryPermission.Read | RepositoryPermission.Manage | RepositoryPermission.Push) };
        Assert.True(await store.ReplaceGrantsAsync(Actor, binding.Id, ["Admin"], 1, grants, Cancellation));
        var resolver = new SourceControlCredentialResolver(store, credentials);
        Assert.Equal("key-before", await resolver.ResolveAsync(Actor, binding.Id, 2, ["Admin"], RepositoryPermission.Push, "GitHubApp", "privateKey", Cancellation));
        await credentials.RotateSecretAsync(Actor.TenantId, credential.Id, new("privateKey", "key-after"), Cancellation);
        Assert.Equal("key-after", await resolver.ResolveAsync(Actor, binding.Id, 2, ["Admin"], RepositoryPermission.Push, "GitHubApp", "privateKey", Cancellation));
        await Assert.ThrowsAsync<SourceControlSecurityException>(() => resolver.ResolveAsync(new("tenant-b", Actor.ActorId), binding.Id,
            2, ["Admin"], RepositoryPermission.Push, "GitHubApp", "privateKey", Cancellation));
        Assert.True(await store.ReplaceGrantsAsync(Actor, binding.Id, ["Admin"], 2,
            [new(Actor.ActorId, RepositoryPermission.Read | RepositoryPermission.Manage)], Cancellation));
        await Assert.ThrowsAsync<SourceControlSecurityException>(() => resolver.ResolveAsync(Actor, binding.Id,
            3, ["Admin"], RepositoryPermission.Push, "GitHubApp", "privateKey", Cancellation));
    }

    private sealed class StoreFixture : IAsyncDisposable
    {
        private readonly string _database = Path.Combine(Path.GetTempPath(), $"vertex-source-control-{Guid.NewGuid():N}.db");
        private readonly IDataProtectionProvider _protection = new EphemeralDataProtectionProvider();
        private readonly IOptions<SourceControlOptions> _options = Options.Create(new SourceControlOptions
        { Enabled = true, AllowedHosts = ["github.com", "api.github.com"] });
        internal BpmnDbContext Db() => new(new DbContextOptionsBuilder<BpmnDbContext>()
            .UseSqlite($"Data Source={_database};Pooling=False").Options);
        internal PersistentSourceControlStore Store(BpmnDbContext db) => new(db, _protection, _options);
        internal SourceControlWorkspace Workspace(BpmnDbContext db, string root) => new(db, _protection,
            Options.Create(new SourceControlOptions { Enabled = true, WorkspaceRoot = root }));
        internal PersistentCredentialService Credentials(BpmnDbContext db) => new(db, _protection,
            Mock.Of<IAuditLogService>(), NullLogger<PersistentCredentialService>.Instance);
        internal RepositoryBinding Binding(string? credential = null) => new(Guid.NewGuid(), Actor.TenantId,
            new Uri("https://github.com/example/models.git"), credential, "master", "release", ["models"]);
        internal async Task<RepositoryBinding> InitializeAsync()
        {
            await using var db = Db();
            await db.Database.MigrateAsync(Cancellation);
            var store = Store(db);
            var binding = Binding();
            await store.CreateBindingAsync(Actor, ["Admin"], binding, Cancellation);
            await store.ReplaceGrantsAsync(Actor, binding.Id, ["Admin"], 1,
                [new(Actor.ActorId, RepositoryPermission.Read | RepositoryPermission.Manage | RepositoryPermission.Commit | RepositoryPermission.Push)], Cancellation);
            return binding;
        }
        public ValueTask DisposeAsync()
        {
            // Exact generated test files only; never a repository or directory cleanup.
            foreach (var path in new[] { _database, _database + "-wal", _database + "-shm" })
                if (File.Exists(path)) File.Delete(path);
            return ValueTask.CompletedTask;
        }
    }
}
