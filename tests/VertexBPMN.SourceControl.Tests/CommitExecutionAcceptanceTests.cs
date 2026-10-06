using System.Diagnostics;
using System.Data.Common;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using VertexBPMN.Application.SourceControl;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Infrastructure.Persistence;
using VertexBPMN.Infrastructure.SourceControl;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.SourceControl.Tests;

public sealed class CommitExecutionAcceptanceTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Accepted_commit_executes_original_bytes_and_multiline_message_after_editor_advances()
    {
        await using var fixture = await Fixture.CreateAsync();
        await using var db = fixture.Db();
        var store = fixture.Store(db);
        var work = await store.ReadCommitWorkAsync(fixture.Actor, fixture.Id, "first", 1, ["Admin"], Cancellation);
        var next = new ModelSnapshot(fixture.Snapshot.Path, SourceModelKind.Bpmn, fixture.Snapshot.DocumentGeneration, 2,
            Encoding.UTF8.GetBytes("<definitions xmlns=\"http://www.omg.org/spec/BPMN/20100524/MODEL\"><process id=\"later\"/></definitions>"));
        Assert.True(await store.SaveSnapshotsAsync(fixture.Actor, work.Command.SessionId, ["Admin"], 1, [next], Cancellation));
        Assert.True(await store.RenewLeaseAsync(fixture.Actor.TenantId, fixture.Id, "first", 1,
            DateTimeOffset.UtcNow, TimeSpan.FromMinutes(5), Cancellation));
        Assert.Equal(work.AcceptedAt, (await store.ReadCommitWorkAsync(fixture.Actor, fixture.Id, "first", 1, ["Admin"], Cancellation)).AcceptedAt);
        var receipt = await fixture.Executor(db).ExecutePreparedAsync(fixture.Actor, fixture.Id, "first", 1, ["Admin"], fixture.Workspace, Cancellation);
        Assert.Equal(fixture.Bytes, await fixture.GitAsync([fixture.GitDir, "show", receipt.Commit.Value + ":models/process.bpmn"]));
        Assert.Equal(fixture.Base.Value, Encoding.UTF8.GetString(await fixture.GitAsync([fixture.GitDir, "rev-parse", receipt.Commit.Value + "^"])).Trim());
        Assert.Equal(receipt.Commit.Value, await fixture.HeadAsync());
        Assert.Equal(2, (await store.ReadSnapshotsAsync(fixture.Actor, work.Command.SessionId, ["Admin"], Cancellation)).Single().LocalRevision);
        var row = await db.SourceControlOperations.AsNoTracking().SingleAsync(Cancellation);
        Assert.Equal((int)SourceControlOperationState.CommittedLocal, row.State);
        Assert.Null(row.LeaseOwner);
        Assert.NotNull(row.ProtectedResult);
        Assert.Null(await store.TryClaimAsync(fixture.Actor.TenantId, fixture.Id, "retry", DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1), Cancellation));
        Assert.Equal("2", Encoding.UTF8.GetString(await fixture.GitAsync([fixture.GitDir, "rev-list", "--count", fixture.Branch])).Trim());
    }

    [Fact]
    public Task Real_git_effect_survives_database_finish_failure_and_new_fence_reconciles_without_another_commit()
        => RunRecoveryAsync(false);

    internal static async Task RunRecoveryAsync(bool postgres)
    {
        await using var fixture = await Fixture.CreateAsync(postgres);
        string head;
        await using (var db = fixture.Db())
        {
            var fault = postgres
                ? "CREATE FUNCTION acceptance_fail_finish() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN IF NEW.\"State\" = "
                    + (int)SourceControlOperationState.CommittedLocal
                    + " THEN RAISE EXCEPTION 'isolated acceptance fault'; END IF; RETURN NEW; END; $$; CREATE TRIGGER fail_commit_finish BEFORE UPDATE OF \"State\" ON \"SourceControlOperations\" FOR EACH ROW EXECUTE FUNCTION acceptance_fail_finish();"
                : $"CREATE TRIGGER fail_commit_finish BEFORE UPDATE OF State ON SourceControlOperations WHEN NEW.State = {(int)SourceControlOperationState.CommittedLocal} BEGIN SELECT RAISE(ABORT, 'isolated acceptance fault'); END;";
            await db.Database.ExecuteSqlRawAsync(fault, Cancellation);
            await Assert.ThrowsAnyAsync<DbException>(() => fixture.Executor(db).ExecutePreparedAsync(fixture.Actor,
                fixture.Id, "first", 1, ["Admin"], fixture.Workspace, Cancellation));
            head = await fixture.HeadAsync();
            var row = await db.SourceControlOperations.AsNoTracking().SingleAsync(Cancellation);
            Assert.Equal((int)SourceControlOperationState.Running, row.State);
            Assert.NotNull(row.ProtectedResult); // Saved and committed BEFORE the ref effect.
            await db.Database.ExecuteSqlRawAsync(postgres
                ? "DROP TRIGGER fail_commit_finish ON \"SourceControlOperations\"; DROP FUNCTION acceptance_fail_finish();"
                : "DROP TRIGGER fail_commit_finish;", Cancellation);
            await fixture.ExpireAsync(db);
        }
        // Fresh DbContext/store/executor; no in-process result cache is available.
        await using var recoveredDb = fixture.Db();
        var store = fixture.Store(recoveredDb);
        Assert.Equal(1, await store.DetectExpiredLeasesAsync(DateTimeOffset.UtcNow, Cancellation));
        Assert.Equal(3L, await store.TryClaimAsync(fixture.Actor.TenantId, fixture.Id, "recovery", DateTimeOffset.UtcNow, TimeSpan.FromMinutes(5), Cancellation));
        var stale = await Assert.ThrowsAsync<SourceControlSecurityException>(() => fixture.Executor(recoveredDb).ExecutePreparedAsync(
            fixture.Actor, fixture.Id, "first", 1, ["Admin"], fixture.Workspace, Cancellation));
        Assert.Equal(SourceControlErrorCode.NotFound, stale.Code);
        var receipt = await fixture.Executor(recoveredDb).ExecutePreparedAsync(fixture.Actor, fixture.Id, "recovery", 3,
            ["Admin"], fixture.Workspace, Cancellation);
        Assert.Equal(head, receipt.Commit.Value);
        Assert.Equal(head, await fixture.HeadAsync());
        Assert.Equal("2", Encoding.UTF8.GetString(await fixture.GitAsync([fixture.GitDir, "rev-list", "--count", fixture.Branch])).Trim());
        Assert.Equal((int)SourceControlOperationState.CommittedLocal, (await recoveredDb.SourceControlOperations.AsNoTracking().SingleAsync(Cancellation)).State);
    }

    [Fact]
    public async Task Recorded_intent_without_ref_is_published_once_under_the_new_fence()
    {
        await using var fixture = await Fixture.CreateAsync();
        GitCommitId expected;
        await using (var db = fixture.Db())
        {
            var store = fixture.Store(db);
            var work = await store.ReadCommitWorkAsync(fixture.Actor, fixture.Id, "first", 1, ["Admin"], Cancellation);
            expected = await fixture.Git.BuildCommitAsync(fixture.Workspace, work.Binding, work.Command, work.AcceptedAt, Cancellation);
            var receipt = new CommitReceipt(fixture.Id, work.Command.SessionId, expected, fixture.Branch,
                [new(fixture.Snapshot.Path, fixture.Snapshot.DocumentGeneration, fixture.Snapshot.LocalRevision, fixture.Snapshot.ContentSha256)]);
            Assert.True(await store.SaveResultAsync(fixture.Actor.TenantId, fixture.Id, "first", 1,
                JsonSerializer.SerializeToUtf8Bytes(new StoredLocalCommit(1, fixture.Binding.Id, 1, receipt)), DateTimeOffset.UtcNow, Cancellation));
            Assert.Null(await fixture.Git.ReadLocalBranchAsync(fixture.Workspace, fixture.Branch, Cancellation));
            await fixture.ExpireAsync(db);
        }
        await using var recovery = fixture.Db();
        Assert.Equal(2L, await fixture.Store(recovery).TryClaimAsync(fixture.Actor.TenantId, fixture.Id, "recovery", DateTimeOffset.UtcNow, TimeSpan.FromMinutes(5), Cancellation));
        var result = await fixture.Executor(recovery).ExecutePreparedAsync(fixture.Actor, fixture.Id, "recovery", 2,
            ["Admin"], fixture.Workspace, Cancellation);
        Assert.Equal(expected, result.Commit);
        Assert.Equal(expected.Value, await fixture.HeadAsync());
    }

    [Fact]
    public async Task Unknown_operation_without_receipt_is_not_blindly_replayed()
    {
        await using var fixture = await Fixture.CreateAsync();
        await using var db = fixture.Db();
        await fixture.ExpireAsync(db);
        Assert.Equal(2L, await fixture.Store(db).TryClaimAsync(fixture.Actor.TenantId, fixture.Id, "recovery", DateTimeOffset.UtcNow, TimeSpan.FromMinutes(5), Cancellation));
        var error = await Assert.ThrowsAsync<SourceControlSecurityException>(() => fixture.Executor(db).ExecutePreparedAsync(
            fixture.Actor, fixture.Id, "recovery", 2, ["Admin"], fixture.Workspace, Cancellation));
        Assert.Equal(SourceControlErrorCode.ResultUnknown, error.Code);
        Assert.Null(await fixture.Git.ReadLocalBranchAsync(fixture.Workspace, fixture.Branch, Cancellation));
        Assert.Null((await db.SourceControlOperations.AsNoTracking().SingleAsync(Cancellation)).ProtectedResult);
    }

    [Fact]
    public async Task Conflicting_local_ref_is_never_overwritten_and_receipt_is_retained()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.GitAsync([fixture.GitDir, "update-ref", "refs/heads/" + fixture.Branch, fixture.Base.Value]);
        await using var db = fixture.Db();
        var error = await Assert.ThrowsAsync<SourceControlSecurityException>(() => fixture.Executor(db).ExecutePreparedAsync(
            fixture.Actor, fixture.Id, "first", 1, ["Admin"], fixture.Workspace, Cancellation));
        Assert.Equal(SourceControlErrorCode.RevisionConflict, error.Code);
        Assert.Equal(fixture.Base.Value, await fixture.HeadAsync());
        Assert.NotNull((await db.SourceControlOperations.AsNoTracking().SingleAsync(Cancellation)).ProtectedResult);
    }

    [Fact]
    public async Task Revoked_acl_and_wrong_actor_or_roles_cannot_execute_queued_work()
    {
        await using var fixture = await Fixture.CreateAsync();
        await using var db = fixture.Db();
        var executor = fixture.Executor(db);
        var foreign = await Assert.ThrowsAsync<SourceControlSecurityException>(() => executor.ExecutePreparedAsync(
            new("other-tenant", fixture.Actor.ActorId), fixture.Id, "first", 1, ["Admin"], fixture.Workspace, Cancellation));
        Assert.Equal(SourceControlErrorCode.NotFound, foreign.Code);
        var role = await Assert.ThrowsAsync<SourceControlSecurityException>(() => executor.ExecutePreparedAsync(
            fixture.Actor, fixture.Id, "first", 1, ["ReadOnly"], fixture.Workspace, Cancellation));
        Assert.Equal(SourceControlErrorCode.Forbidden, role.Code);
        Assert.True(await fixture.Store(db).ReplaceGrantsAsync(fixture.Actor, fixture.Binding.Id, ["Admin"], 2,
            [new(fixture.Actor.ActorId, RepositoryPermission.Read | RepositoryPermission.Manage)], Cancellation));
        var revoked = await Assert.ThrowsAsync<SourceControlSecurityException>(() => executor.ExecutePreparedAsync(
            fixture.Actor, fixture.Id, "first", 1, ["Admin"], fixture.Workspace, Cancellation));
        Assert.Equal(SourceControlErrorCode.Forbidden, revoked.Code);
        Assert.Null(await fixture.Git.ReadLocalBranchAsync(fixture.Workspace, fixture.Branch, Cancellation));
        Assert.Null((await db.SourceControlOperations.AsNoTracking().SingleAsync(Cancellation)).ProtectedResult);
    }

    [Fact]
    public async Task Role_revocation_during_execution_blocks_publication_and_preserves_recovery_intent()
    {
        await using var fixture = await Fixture.CreateAsync();
        await using var db = fixture.Db();
        var resolutions = 0;
        Task<IReadOnlyCollection<string>> Resolve(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyCollection<string>>(++resolutions == 1 ? ["Admin"] : ["ReadOnly"]);
        }
        var error = await Assert.ThrowsAsync<SourceControlSecurityException>(() => fixture.Executor(db).ExecutePreparedAsync(
            fixture.Actor, fixture.Id, "first", 1, Resolve, fixture.Workspace, Cancellation));
        Assert.Equal(SourceControlErrorCode.Forbidden, error.Code);
        Assert.Equal(2, resolutions);
        Assert.Null(await fixture.Git.ReadLocalBranchAsync(fixture.Workspace, fixture.Branch, Cancellation));
        var row = await db.SourceControlOperations.AsNoTracking().SingleAsync(Cancellation);
        Assert.NotNull(row.ProtectedResult);
        Assert.Equal((int)SourceControlOperationState.Running, row.State);
    }

    [Fact]
    public async Task Claimed_runner_renews_in_separate_scopes_and_finishes_real_commit()
    {
        await using var fixture = await Fixture.CreateAsync();
        await using var services = fixture.Services();
        var calls = 0;
        async Task<IReadOnlyCollection<string>> Roles(CancellationToken token)
        {
            if (++calls == 1)
            {
                await using var db = fixture.Db();
                var row = await db.SourceControlOperations.AsNoTracking().SingleAsync(token);
                Assert.InRange(row.LeaseUntilUtcTicks!.Value, DateTimeOffset.UtcNow.AddMinutes(1).UtcTicks,
                    DateTimeOffset.UtcNow.AddMinutes(3).UtcTicks);
                await Task.Delay(TimeSpan.FromMilliseconds(150), token);
            }
            return ["Admin"];
        }
        var outcome = await new SourceControlClaimedCommitRunner(services.GetRequiredService<IServiceScopeFactory>())
            .RunPreparedAsync(fixture.Actor, fixture.Id, "first", 1, Roles, fixture.Workspace,
                TimeSpan.FromMilliseconds(20), Cancellation);
        Assert.Equal(SourceControlOperationState.CommittedLocal, outcome.State);
        Assert.NotNull(outcome.Receipt);
        Assert.Null(outcome.Error);
        Assert.Equal(outcome.Receipt.Commit.Value, await fixture.HeadAsync());
        await using var db = fixture.Db();
        var row = await db.SourceControlOperations.AsNoTracking().SingleAsync(Cancellation);
        Assert.Null(row.LeaseOwner);
        Assert.Equal((int)SourceControlOperationState.CommittedLocal, row.State);
        Assert.Null(row.ErrorCode);
    }

    [Fact]
    public async Task Claimed_runner_denial_before_intent_is_failed_without_git_effect()
    {
        await using var fixture = await Fixture.CreateAsync();
        await using var services = fixture.Services();
        var outcome = await new SourceControlClaimedCommitRunner(services.GetRequiredService<IServiceScopeFactory>())
            .RunPreparedAsync(fixture.Actor, fixture.Id, "first", 1,
                _ => Task.FromResult<IReadOnlyCollection<string>>(["ReadOnly"]), fixture.Workspace,
                TimeSpan.FromMilliseconds(20), Cancellation);
        Assert.Equal(SourceControlOperationState.Failed, outcome.State);
        Assert.Equal(SourceControlErrorCode.Forbidden, outcome.Error);
        await using var db = fixture.Db();
        var row = await db.SourceControlOperations.AsNoTracking().SingleAsync(Cancellation);
        Assert.Equal((int)SourceControlErrorCode.Forbidden, row.ErrorCode);
        Assert.Null(row.ProtectedResult);
        Assert.Null(row.LeaseOwner);
        Assert.Null(await fixture.Git.ReadLocalBranchAsync(fixture.Workspace, fixture.Branch, Cancellation));
    }

    [Fact]
    public async Task Claimed_runner_lost_lease_cannot_finish_or_publish_the_old_claim()
    {
        await using var fixture = await Fixture.CreateAsync();
        await using var services = fixture.Services();
        async Task<IReadOnlyCollection<string>> Roles(CancellationToken token)
        {
            await using var db = fixture.Db();
            await fixture.ExpireAsync(db);
            return ["Admin"];
        }
        var outcome = await new SourceControlClaimedCommitRunner(services.GetRequiredService<IServiceScopeFactory>())
            .RunPreparedAsync(fixture.Actor, fixture.Id, "first", 1, Roles, fixture.Workspace,
                TimeSpan.FromSeconds(1), Cancellation);
        Assert.Equal(SourceControlOperationState.ResultUnknown, outcome.State);
        await using var read = fixture.Db();
        var row = await read.SourceControlOperations.AsNoTracking().SingleAsync(Cancellation);
        Assert.Equal((int)SourceControlOperationState.Running, row.State);
        Assert.Null(row.ProtectedResult);
        Assert.Null(await fixture.Git.ReadLocalBranchAsync(fixture.Workspace, fixture.Branch, Cancellation));
        Assert.Equal(1, await fixture.Store(read).DetectExpiredLeasesAsync(DateTimeOffset.UtcNow, Cancellation));
        Assert.Equal(3L, await fixture.Store(read).TryClaimAsync(fixture.Actor.TenantId, fixture.Id, "new-worker",
            DateTimeOffset.UtcNow, TimeSpan.FromMinutes(2), Cancellation));
    }

    [Fact]
    public async Task Claimed_runner_denial_after_intent_preserves_unknown_effect_for_reconciliation()
    {
        await using var fixture = await Fixture.CreateAsync();
        await using var services = fixture.Services();
        var calls = 0;
        var outcome = await new SourceControlClaimedCommitRunner(services.GetRequiredService<IServiceScopeFactory>())
            .RunPreparedAsync(fixture.Actor, fixture.Id, "first", 1,
                _ => Task.FromResult<IReadOnlyCollection<string>>(++calls == 1 ? ["Admin"] : ["ReadOnly"]),
                fixture.Workspace, TimeSpan.FromMilliseconds(20), Cancellation);
        Assert.Equal(SourceControlOperationState.ResultUnknown, outcome.State);
        await using var db = fixture.Db();
        var row = await db.SourceControlOperations.AsNoTracking().SingleAsync(Cancellation);
        Assert.NotNull(row.ProtectedResult);
        Assert.Equal((int)SourceControlOperationState.ResultUnknown, row.State);
        Assert.Null(await fixture.Git.ReadLocalBranchAsync(fixture.Workspace, fixture.Branch, Cancellation));
    }

    [Fact]
    public Task Claimed_runner_recovers_real_ref_after_database_finish_failure() => RunClaimedRunnerRecoveryAsync(false);

    internal static async Task RunClaimedRunnerRecoveryAsync(bool postgres)
    {
        await using var fixture = await Fixture.CreateAsync(postgres);
        await using var services = fixture.Services();
        var runner = new SourceControlClaimedCommitRunner(services.GetRequiredService<IServiceScopeFactory>());
        Task<IReadOnlyCollection<string>> Roles(CancellationToken _) => Task.FromResult<IReadOnlyCollection<string>>(["Admin"]);
        await using (var db = fixture.Db())
            await db.Database.ExecuteSqlRawAsync(postgres
                ? "CREATE FUNCTION acceptance_fail_runner() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN IF NEW.\"State\" = "
                    + (int)SourceControlOperationState.CommittedLocal
                    + " THEN RAISE EXCEPTION 'isolated fault'; END IF; RETURN NEW; END; $$; CREATE TRIGGER fail_runner_finish BEFORE UPDATE OF \"State\" ON \"SourceControlOperations\" FOR EACH ROW EXECUTE FUNCTION acceptance_fail_runner();"
                : $"CREATE TRIGGER fail_runner_finish BEFORE UPDATE OF State ON SourceControlOperations WHEN NEW.State = {(int)SourceControlOperationState.CommittedLocal} BEGIN SELECT RAISE(ABORT, 'isolated fault'); END;", Cancellation);
        var unknown = await runner.RunPreparedAsync(fixture.Actor, fixture.Id, "first", 1, Roles,
            fixture.Workspace, TimeSpan.FromSeconds(1), Cancellation);
        Assert.Equal(SourceControlOperationState.ResultUnknown, unknown.State);
        var head = await fixture.HeadAsync();
        await using (var db = fixture.Db())
        {
            var row = await db.SourceControlOperations.AsNoTracking().SingleAsync(Cancellation);
            Assert.Equal((int)SourceControlOperationState.ResultUnknown, row.State);
            Assert.NotNull(row.ProtectedResult);
            Assert.Null(row.LeaseOwner);
            await db.Database.ExecuteSqlRawAsync(postgres
                ? "DROP TRIGGER fail_runner_finish ON \"SourceControlOperations\"; DROP FUNCTION acceptance_fail_runner();"
                : "DROP TRIGGER fail_runner_finish;", Cancellation);
            Assert.Equal(2L, await fixture.Store(db).TryClaimAsync(fixture.Actor.TenantId, fixture.Id, "recovery",
                DateTimeOffset.UtcNow, TimeSpan.FromMinutes(2), Cancellation));
        }
        var recovered = await runner.RunPreparedAsync(fixture.Actor, fixture.Id, "recovery", 2, Roles,
            fixture.Workspace, TimeSpan.FromSeconds(1), Cancellation);
        Assert.Equal(SourceControlOperationState.CommittedLocal, recovered.State);
        Assert.Equal(head, recovered.Receipt!.Commit.Value);
        Assert.Equal(head, await fixture.HeadAsync());
        Assert.Equal("2", Encoding.UTF8.GetString(await fixture.GitAsync([fixture.GitDir, "rev-list", "--count", fixture.Branch])).Trim());
        await using var finalDb = fixture.Db();
        Assert.True(await fixture.Store(finalDb).ConfirmCompletedCommitAsync(fixture.Actor, fixture.Id, 2, recovered.Receipt, Cancellation));
        Assert.False(await fixture.Store(finalDb).ConfirmCompletedCommitAsync(fixture.Actor, fixture.Id, 1, recovered.Receipt, Cancellation));
        Assert.False(await fixture.Store(finalDb).ConfirmCompletedCommitAsync(new(fixture.Actor.TenantId, "other-actor"), fixture.Id, 2, recovered.Receipt, Cancellation));
        Assert.False(await fixture.Store(finalDb).ConfirmCompletedCommitAsync(fixture.Actor, fixture.Id, 2,
            recovered.Receipt with { Commit = fixture.Base }, Cancellation));
    }

    [Fact]
    public async Task Claimed_runner_wrong_actor_cannot_finish_another_actors_job()
    {
        await using var fixture = await Fixture.CreateAsync();
        await using var services = fixture.Services();
        var error = await Assert.ThrowsAsync<SourceControlSecurityException>(() =>
            new SourceControlClaimedCommitRunner(services.GetRequiredService<IServiceScopeFactory>())
                .RunPreparedAsync(new(fixture.Actor.TenantId, "other-actor"), fixture.Id, "first", 1,
                    _ => throw new InvalidOperationException("must not resolve"), fixture.Workspace,
                    TimeSpan.FromMilliseconds(20), Cancellation));
        Assert.Equal(SourceControlErrorCode.NotFound, error.Code);
        await using var db = fixture.Db();
        var row = await db.SourceControlOperations.AsNoTracking().SingleAsync(Cancellation);
        Assert.Equal((int)SourceControlOperationState.Running, row.State);
        Assert.Equal("first", row.LeaseOwner);
    }

    [Fact]
    public async Task Symbolic_work_branch_cannot_redirect_a_write_to_the_default_branch()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.GitAsync([fixture.GitDir, "symbolic-ref", "refs/heads/" + fixture.Branch, "refs/heads/master"]);
        await using var db = fixture.Db();
        var error = await Assert.ThrowsAsync<SourceControlSecurityException>(() => fixture.Executor(db).ExecutePreparedAsync(
            fixture.Actor, fixture.Id, "first", 1, ["Admin"], fixture.Workspace, Cancellation));
        Assert.Equal(SourceControlErrorCode.ContentUnsafe, error.Code);
        Assert.Equal(fixture.Base.Value, Encoding.UTF8.GetString(await fixture.GitAsync([fixture.GitDir, "rev-parse", "refs/heads/master"])).Trim());
        Assert.Equal((int)SourceControlOperationState.Running, (await db.SourceControlOperations.AsNoTracking().SingleAsync(Cancellation)).State);
    }

    [Fact]
    public async Task Competing_native_ref_updates_only_publish_one_expected_commit()
    {
        await using var fixture = await Fixture.CreateAsync();
        await using var db = fixture.Db();
        var work = await fixture.Store(db).ReadCommitWorkAsync(fixture.Actor, fixture.Id, "first", 1, ["Admin"], Cancellation);
        var first = await fixture.Git.BuildCommitAsync(fixture.Workspace, work.Binding, work.Command, work.AcceptedAt, Cancellation);
        var second = await fixture.Git.BuildCommitAsync(fixture.Workspace, work.Binding,
            work.Command with { OperationId = Guid.NewGuid(), Message = "Different contender" }, work.AcceptedAt, Cancellation);
        async Task<SourceControlErrorCode?> Publish(GitCommitId commit)
        {
            try { await fixture.Git.PublishLocalCommitAsync(fixture.Workspace, fixture.Branch, commit, Cancellation); return null; }
            catch (SourceControlSecurityException error) { return error.Code; }
        }
        var outcomes = await Task.WhenAll(Publish(first), Publish(second));
        Assert.Single(outcomes, x => x is null);
        Assert.Single(outcomes, x => x == SourceControlErrorCode.RevisionConflict);
        Assert.Contains(await fixture.HeadAsync(), new[] { first.Value, second.Value });
        Assert.Equal(fixture.Base.Value, Encoding.UTF8.GetString(await fixture.GitAsync([fixture.GitDir, "rev-parse", "refs/heads/master"])).Trim());
    }

    [Fact]
    public async Task Incorrect_saved_commit_id_is_rejected_before_publication()
    {
        await using var fixture = await Fixture.CreateAsync();
        await using var db = fixture.Db();
        var store = fixture.Store(db);
        var work = await store.ReadCommitWorkAsync(fixture.Actor, fixture.Id, "first", 1, ["Admin"], Cancellation);
        var incorrect = new CommitReceipt(fixture.Id, work.Command.SessionId, fixture.Base, fixture.Branch,
            [new(fixture.Snapshot.Path, fixture.Snapshot.DocumentGeneration, fixture.Snapshot.LocalRevision, fixture.Snapshot.ContentSha256)]);
        Assert.True(await store.SaveResultAsync(fixture.Actor.TenantId, fixture.Id, "first", 1,
            JsonSerializer.SerializeToUtf8Bytes(new StoredLocalCommit(1, fixture.Binding.Id, 1, incorrect)), DateTimeOffset.UtcNow, Cancellation));
        var error = await Assert.ThrowsAsync<SourceControlSecurityException>(() => fixture.Executor(db).ExecutePreparedAsync(
            fixture.Actor, fixture.Id, "first", 1, ["Admin"], fixture.Workspace, Cancellation));
        Assert.Equal(SourceControlErrorCode.ContentUnsafe, error.Code);
        Assert.Null(await fixture.Git.ReadLocalBranchAsync(fixture.Workspace, fixture.Branch, Cancellation));
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "vertex-commit-job-" + Guid.NewGuid().ToString("N"));
        private readonly IDataProtectionProvider _protection = new EphemeralDataProtectionProvider();
        private readonly IOptions<SourceControlOptions> _options;
        private readonly string _executable;
        private readonly string? _adminConnection;
        private readonly string _postgresDatabase = "vertex_commit_test_" + Guid.NewGuid().ToString("N");
        private bool _createdDatabase;
        internal SourceControlContext Actor { get; } = new("isolated-tenant", "issuer|actor");
        internal byte[] Bytes { get; } = Encoding.UTF8.GetBytes("<definitions xmlns=\"http://www.omg.org/spec/BPMN/20100524/MODEL\">\r\n<process id=\"confirmed\"/>\r\n</definitions>\r\n");
        internal ModelSnapshot Snapshot { get; private set; } = null!;
        internal RepositoryBinding Binding { get; private set; } = null!;
        internal Guid Id { get; private set; }
        internal string Branch { get; private set; } = null!;
        internal GitCommitId Base { get; private set; } = null!;
        internal GitWorkspace Workspace { get; private set; } = null!;
        internal ControlledGitProcess Git { get; }
        internal string GitDir => "--git-dir=" + Path.Combine(Workspace.Directory, "repository.git");
        private Fixture(bool postgres)
        {
            _executable = Environment.GetEnvironmentVariable("VERTEXBPMN_TEST_GIT")!;
            Assert.True(_executable is not null && Path.IsPathFullyQualified(_executable) && File.Exists(_executable));
            if (postgres)
            {
                _adminConnection = Environment.GetEnvironmentVariable("VERTEXBPMN_TEST_POSTGRES_ADMIN");
                Assert.False(string.IsNullOrWhiteSpace(_adminConnection), "Explicit isolated PostgreSQL test connection required.");
            }
            _options = Options.Create(new SourceControlOptions { Enabled = true, GitExecutablePath = _executable,
                WorkspaceRoot = Path.Combine(_root, "owned-workspaces"), AllowedHosts = ["github.com"] });
            Git = new(_options);
        }
        internal BpmnDbContext Db() => new(_adminConnection is null
            ? new DbContextOptionsBuilder<BpmnDbContext>().UseSqlite($"Data Source={Path.Combine(_root, "test.db")};Pooling=False").Options
            : new DbContextOptionsBuilder<BpmnDbContext>().UseVertexNpgsql(new NpgsqlConnectionStringBuilder(_adminConnection)
                { Database = _postgresDatabase, Pooling = false }.ConnectionString).Options);
        internal PersistentSourceControlStore Store(BpmnDbContext db) => new(db, _protection, _options);
        internal ServiceProvider Services() => new ServiceCollection()
            .AddScoped(_ => Db())
            .AddScoped(sp => Store(sp.GetRequiredService<BpmnDbContext>()))
            .AddScoped(sp => Executor(sp.GetRequiredService<BpmnDbContext>()))
            .BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        internal SourceControlWorkspace Workspaces(BpmnDbContext db) => new(db, _protection, _options);
        internal SourceControlCommitExecutor Executor(BpmnDbContext db)
        {
            var store = Store(db);
            return new(store, Workspaces(db), Git, new GitHubAppTokenBroker(new SourceControlCredentialResolver(store, new NoRemoteCredentials()), _options));
        }
        internal static async Task<Fixture> CreateAsync(bool postgres = false)
        {
            var fixture = new Fixture(postgres);
            try { await fixture.InitializeAsync(); return fixture; }
            catch { await fixture.DisposeAsync(); throw; }
        }
        private async Task InitializeAsync()
        {
            Directory.CreateDirectory(_root);
            var source = Path.Combine(_root, "source");
            Directory.CreateDirectory(Path.Combine(source, "models"));
            await File.WriteAllBytesAsync(Path.Combine(source, "models", "process.bpmn"),
                Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(Bytes).Replace("confirmed", "basis", StringComparison.Ordinal)), Cancellation);
            await GitAsync(["init", "--initial-branch=master", "--template=", source]);
            await GitAsync(["-C", source, "add", "--", "models/process.bpmn"]);
            await GitAsync(["-C", source, "-c", "user.name=Acceptance", "-c", "user.email=acceptance@example.invalid", "commit", "-m", "basis"]);
            Base = new(Encoding.UTF8.GetString(await GitAsync(["-C", source, "rev-parse", "HEAD"])).Trim());
            if (_adminConnection is not null)
            {
                await using var admin = new NpgsqlConnection(new NpgsqlConnectionStringBuilder(_adminConnection) { Pooling = false }.ConnectionString);
                await admin.OpenAsync(Cancellation);
                await using var create = new NpgsqlCommand("CREATE DATABASE " + _postgresDatabase, admin);
                await create.ExecuteNonQueryAsync(Cancellation);
                _createdDatabase = true;
            }
            await using var db = Db();
            await db.Database.MigrateAsync(Cancellation);
            var store = Store(db);
            Binding = new(Guid.NewGuid(), Actor.TenantId, new Uri("https://github.com/example/isolated.git"), null, "master", "release", ["models"]);
            await store.CreateBindingAsync(Actor, ["Admin"], Binding, Cancellation);
            Assert.True(await store.ReplaceGrantsAsync(Actor, Binding.Id, ["Admin"], 1,
                [new(Actor.ActorId, RepositoryPermission.Read | RepositoryPermission.Manage | RepositoryPermission.Commit)], Cancellation));
            var generation = Guid.NewGuid();
            var session = await store.CreateSessionAsync(Actor, Binding.Id, ["Admin"], Base, generation, Cancellation);
            Branch = SourceControlInputPolicy.WorkBranch(session);
            Snapshot = new("models/process.bpmn", SourceModelKind.Bpmn, generation, 1, Bytes);
            Assert.True(await store.SaveSnapshotsAsync(Actor, session, ["Admin"], 0, [Snapshot], Cancellation));
            Id = await store.EnqueueCommitAsync(Actor, Binding.Id, ["Admin"], new("execute-once"),
                new(session, 1, Base, Branch, "Confirmed\n\nMultiline message", [Snapshot]), Cancellation);
            Assert.Equal(1L, await store.TryClaimAsync(Actor.TenantId, Id, "first", DateTimeOffset.UtcNow, TimeSpan.FromMinutes(5), Cancellation));
            Workspace = await Workspaces(db).CreateAsync(Actor, Id, "first", 1, Cancellation);
            // Local native setup only. Transport is independently qualified by GitHttpsTransportTests.
            await GitAsync(["clone", "--bare", "--no-local", "--template=", source, Path.Combine(Workspace.Directory, "repository.git")]);
        }
        internal async Task ExpireAsync(BpmnDbContext db) => _ = await db.SourceControlOperations.Where(x => x.Id == Id)
            .ExecuteUpdateAsync(u => u.SetProperty(x => x.LeaseUntilUtcTicks, DateTimeOffset.UtcNow.AddSeconds(-1).UtcTicks), Cancellation);
        internal async Task<string> HeadAsync() => Encoding.UTF8.GetString(await GitAsync([GitDir, "rev-parse", "refs/heads/" + Branch])).Trim();
        internal async Task<byte[]> GitAsync(string[] args)
        {
            var start = new ProcessStartInfo(_executable) { UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true };
            start.Environment.Clear();
            foreach (var name in new[] { "SystemRoot", "WINDIR" })
                if (Environment.GetEnvironmentVariable(name) is { } value) start.Environment[name] = value;
            start.Environment["PATH"] = Path.GetDirectoryName(_executable)!;
            start.Environment["GIT_CONFIG_NOSYSTEM"] = "1";
            start.Environment["GIT_CONFIG_GLOBAL"] = Path.Combine(_root, "absent-global.config");
            foreach (var arg in args) start.ArgumentList.Add(arg);
            using var process = Process.Start(start)!;
            var error = process.StandardError.ReadToEndAsync(Cancellation);
            using var output = new MemoryStream();
            await Task.WhenAll(process.StandardOutput.BaseStream.CopyToAsync(output, Cancellation), process.WaitForExitAsync(Cancellation));
            _ = await error;
            Assert.Equal(0, process.ExitCode);
            return output.ToArray();
        }
        public async ValueTask DisposeAsync()
        {
            if (_createdDatabase)
            {
                await using var admin = new NpgsqlConnection(new NpgsqlConnectionStringBuilder(_adminConnection) { Pooling = false }.ConnectionString);
                await admin.OpenAsync(CancellationToken.None);
                await using var drop = new NpgsqlCommand("DROP DATABASE " + _postgresDatabase + " WITH (FORCE)", admin);
                await drop.ExecuteNonQueryAsync(CancellationToken.None);
            }
            if (Directory.Exists(_root))
            {
                _ = SourceControlWorkspace.MeasureBytes(_root, long.MaxValue);
                foreach (var file in Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories)) File.SetAttributes(file, FileAttributes.Normal);
                Directory.Delete(_root, recursive: true);
            }
        }
    }

    // These local tests must never exchange a token or contact a remote provider.
    private sealed class NoRemoteCredentials : ICredentialService
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
