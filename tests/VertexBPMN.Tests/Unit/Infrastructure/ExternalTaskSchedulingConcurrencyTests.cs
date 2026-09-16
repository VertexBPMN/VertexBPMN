using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Infrastructure.Persistence;

namespace VertexBPMN.Tests.Unit.Infrastructure;

public class ExternalTaskSchedulingConcurrencyTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CommittedSchedulingReplayUsesPersistedIdentityWithoutNewWrites(bool changedInput)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await Database.CreateAsync(ct);
        var (job, wait) = fixture.NewWork();
        await using (var writer = new BpmnDbContext(fixture.Options))
        await using (var transaction = await writer.Database.BeginTransactionAsync(ct))
        {
            await new ExternalTaskSchedulingStore(writer).StageAsync(job, wait, ct);
            await writer.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }

        await using var replay = new BpmnDbContext(fixture.Options);
        await using (var transaction = await replay.Database.BeginTransactionAsync(ct))
        {
            if (changedInput) job.InputSnapshot = "{\"text\":\"different\"}";
            var store = new ExternalTaskSchedulingStore(replay);
            if (changedInput)
            {
                var error = await Assert.ThrowsAsync<InvalidOperationException>(() => store.StageAsync(job, wait, ct));
                Assert.Equal("external_task_scheduling_conflict", error.Message);
            }
            else
            {
                var existing = await store.StageAsync(job, wait, ct);
                Assert.NotSame(job, existing); // Reloaded from DB, not a hit in the original change tracker.
                Assert.Equal(job.Id, existing.Id);
            }
            Assert.False(replay.ChangeTracker.HasChanges());
            Assert.Equal(0, await replay.SaveChangesAsync(ct));
            await transaction.CommitAsync(ct);
        }
        await using var read = new BpmnDbContext(fixture.Options);
        Assert.Equal("{}", (await read.ExternalTaskJobs.SingleAsync(ct)).InputSnapshot);
        Assert.Equal(wait.Id, (await read.ExecutionTokens.SingleAsync(ct)).Id);
        Assert.Single(await read.HistoryEvents.ToListAsync(ct));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StaleOwnerCannotCommitWorkAfterAnotherOwnerCommit(bool cancel)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await Database.CreateAsync(ct);
        await using var stale = new BpmnDbContext(fixture.Options);
        await stale.ProcessInstances.SingleAsync(ct); // Read the original revision before the winner commits.
        await using (var winner = new BpmnDbContext(fixture.Options))
        await using (var transaction = await winner.Database.BeginTransactionAsync(ct))
        {
            if (cancel)
            {
                var owner = await winner.ProcessInstances.SingleAsync(ct);
                owner.Status = ProcessInstanceStatus.Completed;
                owner.Revision++;
            }
            else
            {
                var (job, wait) = fixture.NewWork();
                await new ExternalTaskSchedulingStore(winner).StageAsync(job, wait, ct);
            }
            await winner.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }

        var (losingJob, losingWait) = fixture.NewWork();
        await using (var transaction = await stale.Database.BeginTransactionAsync(ct))
        {
            await new ExternalTaskSchedulingStore(stale).StageAsync(losingJob, losingWait, ct);
            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => stale.SaveChangesAsync(ct));
            await transaction.RollbackAsync(ct);
        }
        await using var read = new BpmnDbContext(fixture.Options);
        Assert.Equal(cancel ? 0 : 1, await read.ExternalTaskJobs.CountAsync(ct));
        Assert.Equal(cancel ? 0 : 1, await read.ExecutionTokens.CountAsync(ct));
        Assert.Equal(cancel ? 0 : 1, await read.HistoryEvents.CountAsync(ct));
        Assert.False(await read.ExternalTaskJobs.AnyAsync(job => job.Id == losingJob.Id, ct));
        Assert.False(await read.ExecutionTokens.AnyAsync(wait => wait.Id == losingWait.Id, ct));
        Assert.Equal(cancel ? ProcessInstanceStatus.Completed : ProcessInstanceStatus.Running,
            (await read.ProcessInstances.SingleAsync(ct)).Status);
    }

    [Theory]
    [InlineData("activity", 2067)]
    [InlineData("wait", 2067)]
    [InlineData("definition", 787)]
    [InlineData("process", 787)]
    public async Task MigratedDatabaseRejectsDuplicateIdentityAndMissingOwners(string violation, int errorCode)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await Database.CreateAsync(ct);
        var (originalJob, originalWait) = fixture.NewWork();
        await using (var seed = new BpmnDbContext(fixture.Options))
        {
            seed.ExecutionTokens.Add(originalWait);
            seed.ExternalTaskJobs.Add(originalJob);
            await seed.SaveChangesAsync(ct);
        }
        await using (var writer = new BpmnDbContext(fixture.Options))
        await using (var transaction = await writer.Database.BeginTransactionAsync(ct))
        {
            var (job, wait) = fixture.NewWork();
            switch (violation)
            {
                case "activity":
                    job.ActivityExecutionId = originalJob.ActivityExecutionId;
                    wait.ActivityExecutionId = originalJob.ActivityExecutionId;
                    break;
                case "wait": job.WaitTokenId = originalWait.Id; break;
                case "definition": job.DefinitionId = Guid.NewGuid(); break;
                case "process": job.ProcessInstanceId = Guid.NewGuid(); break;
            }
            if (violation != "wait") writer.ExecutionTokens.Add(wait);
            writer.ExternalTaskJobs.Add(job); // Bypass the store to exercise the migrated DB constraints.
            var error = await Assert.ThrowsAsync<DbUpdateException>(() => writer.SaveChangesAsync(ct));
            Assert.Equal(errorCode, Assert.IsType<SqliteException>(error.InnerException).SqliteExtendedErrorCode);
            await transaction.RollbackAsync(ct);
        }
        await using var read = new BpmnDbContext(fixture.Options);
        Assert.Equal(originalJob.Id, (await read.ExternalTaskJobs.SingleAsync(ct)).Id);
        Assert.Equal(originalWait.Id, (await read.ExecutionTokens.SingleAsync(ct)).Id);
    }

    private sealed class Database(SqliteConnection connection, DbContextOptions<BpmnDbContext> options,
        Guid definitionId, Guid processId) : IAsyncDisposable
    {
        public DbContextOptions<BpmnDbContext> Options => options;

        public static async Task<Database> CreateAsync(CancellationToken ct)
        {
            var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
            try
            {
                await connection.OpenAsync(ct);
                var options = new DbContextOptionsBuilder<BpmnDbContext>().UseSqlite(connection).Options;
                await using var db = new BpmnDbContext(options);
                await db.Database.MigrateAsync(ct);
                var deployment = new EngineDeployment { Id = Guid.NewGuid(), Name = "concurrency", TenantId = "tenant-a" };
                var definition = new ProcessDefinition { Id = Guid.NewGuid(), Key = "concurrency", Name = "concurrency", DeploymentId = deployment.Id, TenantId = "tenant-a", TenantScope = "tenant-a", Version = 1 };
                var process = new ProcessInstance { Id = Guid.NewGuid(), ProcessDefinitionId = definition.Id, TenantId = "tenant-a" };
                db.EngineDeployments.Add(deployment);
                db.ProcessDefinitions.Add(definition);
                db.ProcessInstances.Add(process);
                await db.SaveChangesAsync(ct);
                return new Database(connection, options, definition.Id, process.Id);
            }
            catch
            {
                await connection.DisposeAsync();
                throw;
            }
        }

        public (ExternalTaskJob Job, ExecutionToken Wait) NewWork()
        {
            var executionId = Guid.NewGuid();
            var wait = new ExecutionToken { Id = Guid.NewGuid(), ProcessInstanceId = processId, CurrentNodeId = "review", NodeType = "serviceTask", State = ExecutionToken.WaitingState, ActivityExecutionId = executionId, ScopeExecutionId = processId };
            var job = new ExternalTaskJob { Id = Guid.NewGuid(), TenantId = "tenant-a", ProcessInstanceId = processId, DefinitionId = definitionId, DefinitionVersion = 1, ActivityId = "review", ActivityExecutionId = executionId, WaitTokenId = wait.Id, ScopeExecutionId = processId, Topic = "test", ContractVersion = "v1", MaxAttempts = 1, CreatedAt = 1, AvailableAt = 1, Deadline = 300001 };
            return (job, wait);
        }

        public ValueTask DisposeAsync() => connection.DisposeAsync();
    }
}
