using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Npgsql;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Engine.Execution;
using VertexBPMN.Infrastructure.Persistence;

namespace VertexBPMN.Tests.Acceptance;

public sealed class ExternalTaskPostgresAcceptanceTests
{
    [Fact]
    [Trait("Category", "ExternalTaskPostgres")]
    public async Task TwoDatabaseWorkersHaveExactlyOneLeaseWinner()
    {
        var ct = TestContext.Current.CancellationToken;
        var adminString = Environment.GetEnvironmentVariable("VERTEXBPMN_TEST_POSTGRES_ADMIN");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(adminString), "Explicit local PostgreSQL test connection required.");
        var database = "a03_lease_" + Guid.NewGuid().ToString("N");
        await using var admin = new NpgsqlConnection(adminString);
        await admin.OpenAsync(ct);
        await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{database}\"", admin))
            await create.ExecuteNonQueryAsync(ct);
        var connectionString = new NpgsqlConnectionStringBuilder(adminString) { Database = database, Pooling = false }.ConnectionString;
        var options = new DbContextOptionsBuilder<BpmnDbContext>().UseVertexNpgsql(connectionString).Options;
        try
        {
            Guid jobId;
            await using (var seed = new BpmnDbContext(options))
            {
                await seed.GetService<IMigrator>().MigrateAsync(cancellationToken: ct);
                var deployment = new EngineDeployment { Id = Guid.NewGuid(), Name = "a03", TenantId = "a" };
                var definition = new ProcessDefinition
                {
                    Id = Guid.NewGuid(), Key = "a03", Name = "a03", TenantId = "a", TenantScope = "a",
                    Version = 1, DeploymentId = deployment.Id
                };
                var process = new ProcessInstance
                {
                    Id = Guid.NewGuid(), ProcessDefinitionId = definition.Id, TenantId = "a",
                    Status = ProcessInstanceStatus.Running, Revision = 1
                };
                var activityExecutionId = Guid.NewGuid();
                var wait = new ExecutionToken
                {
                    Id = Guid.NewGuid(), ProcessInstanceId = process.Id, CurrentNodeId = "work", NodeType = "serviceTask",
                    State = ExecutionToken.WaitingState, ActivityExecutionId = activityExecutionId,
                    ScopeExecutionId = process.Id, Revision = 1
                };
                var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                var job = new ExternalTaskJob
                {
                    Id = Guid.NewGuid(), TenantId = "a", ProcessInstanceId = process.Id, DefinitionId = definition.Id,
                    DefinitionVersion = 1, ActivityId = "work", ActivityExecutionId = activityExecutionId,
                    WaitTokenId = wait.Id, ScopeExecutionId = process.Id, Topic = "test.work", ContractVersion = "v1",
                    InputSnapshot = "{\"text\":\"synthetic\"}",
                    SchemaSnapshot = "{\"dialect\":\"vertex.scalar-contract.v1\"}",
                    State = ExternalTaskState.Ready, Revision = 1, CreatedAt = now, AvailableAt = now,
                    Deadline = now + 300_000, MaxAttempts = 2
                };
                jobId = job.Id;
                seed.AddRange(deployment, definition, process, wait, job);
                await seed.SaveChangesAsync(ct);
            }

            var barrier = new LeaseCandidateBarrier();
            var workerOptions = new DbContextOptionsBuilder<BpmnDbContext>()
                .UseVertexNpgsql(connectionString).AddInterceptors(barrier).Options;
            var configuration = Unit.Infrastructure.ExternalTaskContractResolverTests.Configuration();
            configuration["ExternalTasks:Contracts:0:MaxAttempts"] = "2";
            async Task<(string Worker, IReadOnlyList<ExternalTaskLease> Leases)> ClaimAsync(string subject)
            {
                await using var workerDb = new BpmnDbContext(workerOptions);
                var service = new ExternalTaskLeaseService(workerDb,
                    new ConfiguredExternalTaskContractResolver(configuration));
                var worker = new ExternalTaskWorkerContext("issuer", subject, "a",
                    new HashSet<string>(["test.work"], StringComparer.Ordinal),
                    new HashSet<string>(StringComparer.Ordinal));
                return (subject, await service.ClaimAsync(worker,
                    new ExternalTaskClaimCommand(["test.work"], 1, 60), ct));
            }

            var claims = await Task.WhenAll(ClaimAsync("worker-a"), ClaimAsync("worker-b"))
                .WaitAsync(TimeSpan.FromSeconds(45), ct);
            Assert.Equal(2, barrier.Arrivals);
            Assert.Single(claims, item => item.Leases.Count == 1);
            Assert.Single(claims, item => item.Leases.Count == 0);
            var winner = claims.Single(item => item.Leases.Count == 1);
            var lease = Assert.Single(winner.Leases);

            await using var read = new BpmnDbContext(options);
            var stored = await read.ExternalTaskJobs.AsNoTracking().SingleAsync(ct);
            Assert.Equal(jobId, stored.Id);
            Assert.Equal(ExternalTaskState.Leased, stored.State);
            Assert.Equal(winner.Worker, stored.WorkerSubject);
            Assert.Equal(1, stored.LeaseGeneration);
            Assert.Equal(1, stored.AttemptsStarted);
            var attempt = await read.ExternalTaskAttempts.AsNoTracking().SingleAsync(ct);
            Assert.Equal(lease.LeaseId, attempt.LeaseId);
            Assert.Equal(winner.Worker, attempt.WorkerSubject);

            await read.ExternalTaskJobs.Where(item => item.Id == jobId).ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.LeaseExpiresAt, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - 1), ct);
            await using var reclaimDb = new BpmnDbContext(options);
            var reclaimService = new ExternalTaskLeaseService(reclaimDb,
                new ConfiguredExternalTaskContractResolver(configuration));
            var workerC = new ExternalTaskWorkerContext("issuer", "worker-c", "a",
                new HashSet<string>(["test.work"], StringComparer.Ordinal), new HashSet<string>(StringComparer.Ordinal));
            var reclaimed = Assert.Single(await reclaimService.ClaimAsync(workerC,
                new ExternalTaskClaimCommand(["test.work"], 1, 60), ct));
            Assert.Equal(2, reclaimed.LeaseGeneration);
            Assert.NotEqual(lease.LeaseId, reclaimed.LeaseId);
            var formerWorker = new ExternalTaskWorkerContext("issuer", winner.Worker, "a",
                new HashSet<string>(["test.work"], StringComparer.Ordinal), new HashSet<string>(StringComparer.Ordinal));
            var stale = await Assert.ThrowsAsync<ExternalTaskLeaseException>(async () =>
                await reclaimService.HeartbeatAsync(formerWorker, jobId,
                    new ExternalTaskHeartbeatCommand(lease.LeaseId, lease.LeaseGeneration), ct));
            Assert.Equal("external_task_not_found", stale.Code);
            reclaimDb.ChangeTracker.Clear();
            var reclaimedAttempts = await reclaimDb.ExternalTaskAttempts.OrderBy(item => item.AttemptNumber).ToListAsync(ct);
            Assert.Equal(2, reclaimedAttempts.Count);
            Assert.Equal("lease_expired", reclaimedAttempts[0].EndReason);
            Assert.Null(reclaimedAttempts[1].EndedAt);
        }
        finally
        {
            using var cleanupTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            await using var drop = new NpgsqlCommand($"DROP DATABASE \"{database}\" WITH (FORCE)", admin);
            await drop.ExecuteNonQueryAsync(cleanupTimeout.Token);
        }
    }

    [Theory]
    [Trait("Category", "ExternalTaskPostgres")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MigratedPostgresSchedulesOneWaitForConcurrentPublicStarts(bool upgrade)
    {
        var ct = TestContext.Current.CancellationToken;
        var adminString = Environment.GetEnvironmentVariable("VERTEXBPMN_TEST_POSTGRES_ADMIN");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(adminString), "Explicit local PostgreSQL test connection required.");
        var database = "a02_external_" + Guid.NewGuid().ToString("N");
        await using var admin = new NpgsqlConnection(adminString);
        await admin.OpenAsync(ct);
        await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{database}\"", admin))
            await create.ExecuteNonQueryAsync(ct);
        var connectionString = new NpgsqlConnectionStringBuilder(adminString) { Database = database, Pooling = false }.ConnectionString;
        var options = new DbContextOptionsBuilder<BpmnDbContext>().UseVertexNpgsql(connectionString).Options;
        try
        {
            ProcessDefinition definition;
            await using (var seed = new BpmnDbContext(options))
            {
                var migrator = seed.GetService<IMigrator>();
                if (upgrade) await migrator.MigrateAsync("20260908090000_NormalizePostgresOAuth2FlowStateTimes", ct);
                var deployment = new EngineDeployment { Id = Guid.NewGuid(), Name = "a02", TenantId = "a" };
                definition = new ProcessDefinition
                {
                    Id = Guid.NewGuid(), Key = "a02", Name = "a02", TenantId = "a", TenantScope = "a", Version = 1, DeploymentId = deployment.Id,
                    BpmnXml = """
                    <definitions xmlns="http://www.omg.org/spec/BPMN/20100524/MODEL" xmlns:v="https://vertexbpmn.io/schema/bpmn/1.0">
                    <process id="a02"><startEvent id="s"/><serviceTask id="work"><extensionElements>
                    <v:externalTask topic="test.work" maxRetries="0" deadlineSeconds="300"/>
                    <v:ioMapping><v:input name="text" expression="document"/></v:ioMapping>
                    </extensionElements></serviceTask><endEvent id="e"/>
                    <sequenceFlow id="f1" sourceRef="s" targetRef="work"/><sequenceFlow id="f2" sourceRef="work" targetRef="e"/>
                    </process></definitions>
                    """
                };
                if (!upgrade) await migrator.MigrateAsync(cancellationToken: ct);
                seed.EngineDeployments.Add(deployment);
                seed.ProcessDefinitions.Add(definition);
                await seed.SaveChangesAsync(ct);
                if (upgrade) await migrator.MigrateAsync(cancellationToken: ct);
                Assert.Equal(definition.BpmnXml, (await seed.ProcessDefinitions.AsNoTracking().SingleAsync(ct)).BpmnXml);
            }

            // Both independent connections miss the inbox lookup, then reach claim INSERT.
            // No sleeps: the pre-save barrier establishes the interleaving deterministically.
            var gate = new ClaimBarrier();
            var concurrentOptions = new DbContextOptionsBuilder<BpmnDbContext>().UseVertexNpgsql(connectionString).AddInterceptors(gate).Options;
            async Task<Guid> StartAsync()
            {
                await using var db = new BpmnDbContext(concurrentOptions);
                var registry = new Mock<IServiceTaskRegistry>(MockBehavior.Strict);
                var config = Unit.Infrastructure.ExternalTaskContractResolverTests.Configuration();
                var runtime = new PersistentProcessExecutionRuntime(db, registry.Object, Mock.Of<IDecisionService>(),
                    NullLogger<PersistentProcessExecutionRuntime>.Instance, config, new ConfiguredExternalTaskContractResolver(config));
                var result = await runtime.StartAsync(definition, new Dictionary<string, object> { ["document"] = "synthetic" }, null, "a", "same-start", ct);
                registry.VerifyNoOtherCalls();
                return result.Id;
            }
            var results = await Task.WhenAll(StartAsync(), StartAsync()).WaitAsync(TimeSpan.FromSeconds(45), ct);
            Assert.Equal(results[0], results[1]);
            Assert.Equal(2, gate.Arrivals);
            await using var read = new BpmnDbContext(options);
            var job = await read.ExternalTaskJobs.SingleAsync(ct);
            var wait = await read.ExecutionTokens.SingleAsync(ct);
            Assert.Equal(results[0], job.ProcessInstanceId);
            Assert.Equal(wait.Id, job.WaitTokenId);
            Assert.Equal(job.ActivityExecutionId, wait.ActivityExecutionId);
            Assert.Equal(ExternalTaskState.Ready, job.State);
            Assert.Equal(ExecutionToken.WaitingState, wait.State);
            Assert.Equal("{\"text\":\"synthetic\"}", job.InputSnapshot);
            Assert.Equal(300000, job.Deadline - job.CreatedAt);
            Assert.Single(await read.ProcessInstances.ToListAsync(ct));
            Assert.Single(await read.RuntimeInbox.ToListAsync(ct));
            Assert.Single(await read.HistoryEvents.Where(item => item.EventType == "EXTERNAL_TASK_CREATED").ToListAsync(ct));

            await using (var rollback = new BpmnDbContext(options))
            await using (var transaction = await rollback.Database.BeginTransactionAsync(ct))
            {
                var staged = System.Text.Json.JsonSerializer.Deserialize<ExternalTaskJob>(System.Text.Json.JsonSerializer.Serialize(job))!;
                staged.Id = Guid.NewGuid();
                staged.ActivityExecutionId = Guid.NewGuid();
                staged.WaitTokenId = Guid.NewGuid();
                var stagedWait = new ExecutionToken
                {
                    Id = staged.WaitTokenId, ProcessInstanceId = staged.ProcessInstanceId, CurrentNodeId = staged.ActivityId,
                    NodeType = "serviceTask", State = ExecutionToken.WaitingState,
                    ActivityExecutionId = staged.ActivityExecutionId, ScopeExecutionId = staged.ScopeExecutionId
                };
                await new ExternalTaskSchedulingStore(rollback).StageAsync(staged, stagedWait, ct);
                await rollback.SaveChangesAsync(ct);
                Assert.Equal(2, await rollback.ExternalTaskJobs.CountAsync(ct));
                await transaction.RollbackAsync(ct);
            }
            Assert.Single(await read.ExternalTaskJobs.AsNoTracking().ToListAsync(ct));
            Assert.Single(await read.ExecutionTokens.AsNoTracking().ToListAsync(ct));
            Assert.Single(await read.HistoryEvents.Where(item => item.EventType == "EXTERNAL_TASK_CREATED").ToListAsync(ct));

            var guard = await Assert.ThrowsAsync<PostgresException>(() => read.GetService<IMigrator>()
                .MigrateAsync("20260908090000_NormalizePostgresOAuth2FlowStateTimes", ct));
            Assert.Equal("23514", guard.SqlState);
            Assert.Equal(job.Id, (await read.ExternalTaskJobs.AsNoTracking().SingleAsync(ct)).Id);
            read.ExternalTaskJobs.Remove(job);
            await read.SaveChangesAsync(ct);
            await read.GetService<IMigrator>().MigrateAsync("20260908090000_NormalizePostgresOAuth2FlowStateTimes", ct);
            Assert.Equal(definition.Id, (await read.ProcessDefinitions.SingleAsync(ct)).Id);
        }
        finally
        {
            // The only deletion target is the GUID-named database created by this test.
            using var cleanupTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            await using var drop = new NpgsqlCommand($"DROP DATABASE \"{database}\" WITH (FORCE)", admin);
            await drop.ExecuteNonQueryAsync(cleanupTimeout.Token);
        }
    }

    private sealed class ClaimBarrier : SaveChangesInterceptor
    {
        private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _arrivals;
        public int Arrivals => _arrivals;
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context!.ChangeTracker.Entries<RuntimeInboxMessage>().Any(entry => entry.State == EntityState.Added))
            {
                if (Interlocked.Increment(ref _arrivals) == 2) _ready.TrySetResult();
                await _ready.Task.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken);
            }
            return result;
        }
    }

    private sealed class LeaseCandidateBarrier : DbCommandInterceptor
    {
        private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _arrivals;
        public int Arrivals => _arrivals;

        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("ExternalTaskJobs", StringComparison.Ordinal)
                && command.CommandText.Contains("ORDER BY", StringComparison.Ordinal)
                && command.CommandText.Contains("LIMIT", StringComparison.Ordinal))
            {
                if (Interlocked.Increment(ref _arrivals) == 2) _ready.TrySetResult();
                await _ready.Task.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken);
            }
            return result;
        }
    }
}
