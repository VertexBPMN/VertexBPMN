using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Infrastructure.Persistence;

namespace VertexBPMN.Tests.Unit.Infrastructure;

public class ExternalTaskPersistenceTests
{
    [Fact]
    public void PostgresMigrationUsesUuidAndBigintWithoutConnecting()
    {
        using var db = new BpmnDbContext(new DbContextOptionsBuilder<BpmnDbContext>()
            .UseNpgsql("Host=localhost;Database=unused;Username=unused").Options);
        var script = db.GetService<IMigrator>().GenerateScript(
            "20260908090000_NormalizePostgresOAuth2FlowStateTimes", "20260912090238_ExternalTaskPersistence");
        Assert.Contains("\"ActivityExecutionId\" uuid", script);
        Assert.Contains("\"Deadline\" bigint", script);
        Assert.Contains("\"LeaseGeneration\" bigint", script);
    }

    [Fact]
    public async Task SqliteUpgradesExistingSchemaAndCanDowngradeEmptyExternalTables()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(ct);
        var options = new DbContextOptionsBuilder<BpmnDbContext>().UseSqlite(connection).Options;
        await using var db = new BpmnDbContext(options);
        var migrator = db.GetService<IMigrator>();
        const string previous = "20260908090000_NormalizePostgresOAuth2FlowStateTimes";
        await migrator.MigrateAsync(previous, ct);
        var users = await db.Users.CountAsync(ct);
        await migrator.MigrateAsync(cancellationToken: ct);
        Assert.Equal(users, await db.Users.CountAsync(ct));
        Assert.Empty(await db.ExternalTaskJobs.ToListAsync(ct));
        Assert.Empty(await db.ExternalTaskAttempts.ToListAsync(ct));
        Assert.Empty(await db.ExternalTaskContinuations.ToListAsync(ct));
        await migrator.MigrateAsync(previous, ct);
        Assert.Equal(users, await db.Users.CountAsync(ct));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task JobAndWaitCommitOrRollbackTogether(bool commit)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(ct);
        var options = new DbContextOptionsBuilder<BpmnDbContext>().UseSqlite(connection).Options;
        await using var db = new BpmnDbContext(options);
        await db.Database.EnsureCreatedAsync(ct);
        var deployment = new EngineDeployment { Id = Guid.NewGuid(), Name = "external-test", TenantId = "tenant-a" };
        var definition = new ProcessDefinition { Id = Guid.NewGuid(), Key = "test", Name = "test", DeploymentId = deployment.Id, TenantId = "tenant-a", TenantScope = "tenant-a", Version = 1 };
        var process = new ProcessInstance { Id = Guid.NewGuid(), ProcessDefinitionId = definition.Id, TenantId = "tenant-a" };
        db.EngineDeployments.Add(deployment);
        db.ProcessDefinitions.Add(definition);
        db.ProcessInstances.Add(process);
        await db.SaveChangesAsync(ct);
        var executionId = Guid.NewGuid();
        var wait = new ExecutionToken { Id = Guid.NewGuid(), ProcessInstanceId = process.Id, CurrentNodeId = "review", NodeType = "serviceTask", State = ExecutionToken.WaitingState, ActivityExecutionId = executionId, ScopeExecutionId = process.Id };
        var job = new ExternalTaskJob { Id = Guid.NewGuid(), TenantId = "tenant-a", ProcessInstanceId = process.Id, DefinitionId = definition.Id, DefinitionVersion = 1, ActivityId = "review", ActivityExecutionId = executionId, WaitTokenId = wait.Id, ScopeExecutionId = process.Id, Topic = "test", ContractVersion = "v1", MaxAttempts = 1, CreatedAt = 1, AvailableAt = 1, Deadline = 300001 };
        var store = new ExternalTaskSchedulingStore(db);
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.StageAsync(job, wait, ct));
        await using (var transaction = await db.Database.BeginTransactionAsync(ct))
        {
            job.TenantId = "tenant-b";
            var rejected = await Assert.ThrowsAsync<InvalidOperationException>(() => store.StageAsync(job, wait, ct));
            Assert.Equal("external_task_invalid_process", rejected.Message);
            Assert.Empty(db.ExternalTaskJobs.Local);
            job.TenantId = "tenant-a";
            await store.StageAsync(job, wait, ct);
            await db.SaveChangesAsync(ct);
            Assert.Same(job, await store.StageAsync(job, wait, ct));
            var conflicting = System.Text.Json.JsonSerializer.Deserialize<ExternalTaskJob>(System.Text.Json.JsonSerializer.Serialize(job))!;
            conflicting.MaxAttempts = 2;
            var conflict = await Assert.ThrowsAsync<InvalidOperationException>(() => store.StageAsync(conflicting, wait, ct));
            Assert.Equal("external_task_scheduling_conflict", conflict.Message);
            Assert.Equal(1, job.MaxAttempts);
            if (commit) await transaction.CommitAsync(ct);
            else await transaction.RollbackAsync(ct);
        }
        await using var read = new BpmnDbContext(options);
        Assert.Equal(commit ? 1 : 0, await read.ExternalTaskJobs.CountAsync(ct));
        Assert.Equal(commit ? 1 : 0, await read.ExecutionTokens.CountAsync(ct));
        Assert.Equal(commit ? 1 : 0, await read.HistoryEvents.CountAsync(ct));
        if (commit)
        {
            Assert.Equal(300001, (await read.ExternalTaskJobs.SingleAsync(ct)).Deadline);
            Assert.Equal(executionId, (await read.ExecutionTokens.SingleAsync(ct)).ActivityExecutionId);
        }
    }

    [Theory]
    [InlineData("\"MaxAttempts\" = 0", "CK_ExternalTaskJobs_Attempts")]
    [InlineData("\"AttemptsStarted\" = 2", "CK_ExternalTaskJobs_Attempts")]
    [InlineData("\"Deadline\" = 1", "CK_ExternalTaskJobs_Time")]
    [InlineData("\"AvailableAt\" = 0", "CK_ExternalTaskJobs_Time")]
    [InlineData("\"Revision\" = -1", "CK_ExternalTaskJobs_Revision")]
    [InlineData("\"LeaseGeneration\" = -1", "CK_ExternalTaskJobs_Revision")]
    [InlineData("\"State\" = 'Unknown'", "CK_ExternalTaskJobs_State")]
    [InlineData("\"State\" = 'Leased'", "CK_ExternalTaskJobs_Lease")]
    [InlineData(null, null)]
    public async Task MigratedSchemaRejectsInvalidJobsAndDestructiveDowngrade(string? assignment, string? constraint)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync(ct);
        var options = new DbContextOptionsBuilder<BpmnDbContext>().UseSqlite(connection).Options;
        await using var db = new BpmnDbContext(options);
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync(cancellationToken: ct);
        var deployment = new EngineDeployment { Id = Guid.NewGuid(), Name = "migration-test", TenantId = "tenant-a" };
        var definition = new ProcessDefinition { Id = Guid.NewGuid(), Key = "migration-test", Name = "test", DeploymentId = deployment.Id, TenantId = "tenant-a", TenantScope = "tenant-a", Version = 1 };
        var process = new ProcessInstance { Id = Guid.NewGuid(), ProcessDefinitionId = definition.Id, TenantId = "tenant-a" };
        db.EngineDeployments.Add(deployment);
        db.ProcessDefinitions.Add(definition);
        db.ProcessInstances.Add(process);
        var executionId = Guid.NewGuid();
        var wait = new ExecutionToken { Id = Guid.NewGuid(), ProcessInstanceId = process.Id, CurrentNodeId = "review", NodeType = "serviceTask", State = ExecutionToken.WaitingState, ActivityExecutionId = executionId, ScopeExecutionId = process.Id };
        var job = new ExternalTaskJob { Id = Guid.NewGuid(), TenantId = "tenant-a", ProcessInstanceId = process.Id, DefinitionId = definition.Id, DefinitionVersion = 1, ActivityId = "review", ActivityExecutionId = executionId, WaitTokenId = wait.Id, ScopeExecutionId = process.Id, Topic = "test", ContractVersion = "v1", MaxAttempts = 1, CreatedAt = 1, AvailableAt = 1, Deadline = 300001 };
        db.ExecutionTokens.Add(wait);
        db.ExternalTaskJobs.Add(job);
        await db.SaveChangesAsync(ct);

        if (assignment is not null)
        {
            // Only fixed InlineData SQL is used: deliberately bypass application validation.
            using var command = connection.CreateCommand();
            command.CommandText = "UPDATE \"ExternalTaskJobs\" SET " + assignment;
            var error = await Assert.ThrowsAsync<SqliteException>(() => command.ExecuteNonQueryAsync(ct));
            Assert.Equal(19, error.SqliteErrorCode);
            Assert.Contains(constraint!, error.Message);
        }
        else
        {
            var error = await Assert.ThrowsAsync<SqliteException>(() => migrator.MigrateAsync(
                "20260908090000_NormalizePostgresOAuth2FlowStateTimes", ct));
            Assert.Equal(19, error.SqliteErrorCode);
            Assert.Contains("CountValue", error.Message);
            Assert.Contains("20260912090238_ExternalTaskPersistence", await db.Database.GetAppliedMigrationsAsync(ct));
        }

        await using var read = new BpmnDbContext(options);
        var retained = await read.ExternalTaskJobs.SingleAsync(ct);
        Assert.Equal(job.Id, retained.Id);
        Assert.Equal(ExternalTaskState.Ready, retained.State);
        Assert.Equal(1, retained.MaxAttempts);
        Assert.Equal(0, retained.AttemptsStarted);
        Assert.Equal(job.Revision, retained.Revision);
        Assert.Equal(0, retained.LeaseGeneration);
        Assert.Equal(1, retained.AvailableAt);
        Assert.Equal(300001, retained.Deadline);
        Assert.Equal(executionId, (await read.ExecutionTokens.SingleAsync(ct)).ActivityExecutionId);
        Assert.Empty(await read.ExternalTaskAttempts.ToListAsync(ct));
        Assert.Empty(await read.ExternalTaskContinuations.ToListAsync(ct));
        if (assignment is null)
        {
            // The failed downgrade must also roll back its temporary guard and release
            // the migration lock, so a later deliberate empty-table downgrade can succeed.
            read.ExternalTaskJobs.Remove(retained);
            await read.SaveChangesAsync(ct);
            await migrator.MigrateAsync("20260908090000_NormalizePostgresOAuth2FlowStateTimes", ct);
            Assert.DoesNotContain("20260912090238_ExternalTaskPersistence", await db.Database.GetAppliedMigrationsAsync(ct));
            Assert.Equal(1, await read.ProcessInstances.CountAsync(ct));
        }
    }
}
