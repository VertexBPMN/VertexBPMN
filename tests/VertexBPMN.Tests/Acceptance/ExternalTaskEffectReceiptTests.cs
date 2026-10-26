using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Engine.Execution;
using VertexBPMN.Infrastructure.Persistence;

namespace VertexBPMN.Tests.Acceptance;

/// <summary>
/// Server-side trusted business-effect receipt (LC08 final slice). The server persists exactly
/// one effect-receipt row bound to the stable activity-execution effect key whenever a completion
/// is accepted, regardless of double completion, response loss, reclaim/retry or reused
/// completions — while lease loss records none. No local adapter confirmation is required here:
/// the completion itself is the server-confirmed effect.
/// </summary>
public sealed class ExternalTaskEffectReceiptTests
{
    [Fact]
    public async Task DoubleCompletionAndResponseLossRecordExactlyOneServerEffectRow()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await Fixture.CreateAsync(SuccessProcess(), ct);
        await fixture.Runtime.StartAsync(fixture.Definition,
            new Dictionary<string, object>(StringComparer.Ordinal) { ["document"] = "synthetic" }, null, "a", "start", ct);
        fixture.Db.ChangeTracker.Clear();
        var worker = Worker();
        var lease = Assert.Single(await fixture.Leases.ClaimAsync(worker,
            new ExternalTaskClaimCommand(["test.work"]), ct));
        using var result = JsonDocument.Parse("{\"approved\":true}");
        var completion = new ExternalTaskCompleteCommand(lease.LeaseId, lease.LeaseGeneration, Guid.NewGuid(),
            result.RootElement.Clone());

        // First delivery commits the effect.
        await fixture.Leases.CompleteAsync(worker, lease.JobId, completion, ct);
        // Response loss: the worker re-delivers the identical completion (idempotent, no new row).
        await fixture.Leases.CompleteAsync(worker, lease.JobId, completion, ct);
        // A re-delivery with a different completionId is a genuine conflict (409), no new row.
        using var result2 = JsonDocument.Parse("{\"approved\":true}");
        var conflict = await Assert.ThrowsAsync<ExternalTaskLeaseException>(async () =>
            await fixture.Leases.CompleteAsync(worker, lease.JobId,
                new ExternalTaskCompleteCommand(lease.LeaseId, lease.LeaseGeneration, Guid.NewGuid(),
                    result2.RootElement.Clone()), ct));
        Assert.Equal("completion_conflict", conflict.Code);
        fixture.Db.ChangeTracker.Clear();

        var receipt = Assert.Single(await fixture.Db.ExternalTaskEffectReceipts.ToListAsync(ct));
        Assert.Equal(lease.JobId, receipt.JobId);
        Assert.Equal(lease.ActivityExecutionId, receipt.ActivityExecutionId);
        Assert.Equal($"external-task:{lease.ActivityExecutionId:N}:primary", receipt.EffectKey);
        Assert.Equal("primary", receipt.EffectName);
        Assert.NotEqual(Guid.Empty, receipt.CompletionId);
        Assert.False(string.IsNullOrWhiteSpace(receipt.ResultHash));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ReclaimAfterStateLossKeepsOldCompletionInertAndRecordsOneRow(bool crashed)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await Fixture.CreateAsync(
            SuccessProcess().Replace("maxRetries=\"0\"", "maxRetries=\"1\"", StringComparison.Ordinal), ct,
            configuration => configuration["ExternalTasks:Contracts:0:MaxAttempts"] = "2");
        await fixture.Runtime.StartAsync(fixture.Definition,
            new Dictionary<string, object>(StringComparer.Ordinal) { ["document"] = "synthetic" }, null, "a", "reclaim", ct);
        fixture.Db.ChangeTracker.Clear();
        var worker = Worker();
        var lease = Assert.Single(await fixture.Leases.ClaimAsync(worker,
            new ExternalTaskClaimCommand(["test.work"]), ct));
        using var result = JsonDocument.Parse("{\"approved\":true}");
        var oldCompletion = new ExternalTaskCompleteCommand(lease.LeaseId, lease.LeaseGeneration, Guid.NewGuid(),
            result.RootElement.Clone());

        var reclaimed = await ReclaimAsync(fixture, lease, crashed, ct);
        Assert.Equal(2, reclaimed.LeaseGeneration);
        Assert.Equal(lease.ActivityExecutionId, reclaimed.ActivityExecutionId);

        // Stale completion (generation 1 lease) after reclaim is 409 lease_lost, no effect row.
        var stale = await Assert.ThrowsAsync<ExternalTaskLeaseException>(async () =>
            await fixture.Leases.CompleteAsync(worker, lease.JobId, oldCompletion, ct));
        Assert.Equal("lease_lost", stale.Code);
        Assert.Empty(await fixture.Db.ExternalTaskEffectReceipts.ToListAsync(ct));

        // Fresh completion on the reclaim is accepted and records exactly one row.
        await fixture.Leases.CompleteAsync(worker, lease.JobId,
            new ExternalTaskCompleteCommand(reclaimed.LeaseId, reclaimed.LeaseGeneration, Guid.NewGuid(),
                result.RootElement.Clone()), ct);
        fixture.Db.ChangeTracker.Clear();

        var receipt = Assert.Single(await fixture.Db.ExternalTaskEffectReceipts.ToListAsync(ct));
        Assert.Equal(reclaimed.ActivityExecutionId, receipt.ActivityExecutionId);
        Assert.Equal(lease.JobId, receipt.JobId);
    }

    [Fact]
    public async Task EarlyOrReusedCompletionWithoutValidLeaseRecordsNoEffectRow()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await Fixture.CreateAsync(SuccessProcess(), ct);
        await fixture.Runtime.StartAsync(fixture.Definition,
            new Dictionary<string, object>(StringComparer.Ordinal) { ["document"] = "synthetic" }, null, "a", "early", ct);
        fixture.Db.ChangeTracker.Clear();
        var worker = Worker();
        var lease = Assert.Single(await fixture.Leases.ClaimAsync(worker,
            new ExternalTaskClaimCommand(["test.work"]), ct));
        using var result = JsonDocument.Parse("{\"approved\":true}");

        // Wrong lease id -> lease lost, no row.
        await Assert.ThrowsAsync<ExternalTaskLeaseException>(async () =>
            await fixture.Leases.CompleteAsync(worker, lease.JobId,
                new ExternalTaskCompleteCommand(Guid.NewGuid(), lease.LeaseGeneration, Guid.NewGuid(),
                    result.RootElement.Clone()), ct));
        // Wrong generation -> lease lost, no row.
        await Assert.ThrowsAsync<ExternalTaskLeaseException>(async () =>
            await fixture.Leases.CompleteAsync(worker, lease.JobId,
                new ExternalTaskCompleteCommand(lease.LeaseId, lease.LeaseGeneration + 1, Guid.NewGuid(),
                    result.RootElement.Clone()), ct));
        Assert.Empty(await fixture.Db.ExternalTaskEffectReceipts.ToListAsync(ct));

        // Valid completion now records exactly one row.
        await fixture.Leases.CompleteAsync(worker, lease.JobId,
            new ExternalTaskCompleteCommand(lease.LeaseId, lease.LeaseGeneration, Guid.NewGuid(),
                result.RootElement.Clone()), ct);
        fixture.Db.ChangeTracker.Clear();
        Assert.Single(await fixture.Db.ExternalTaskEffectReceipts.ToListAsync(ct));
    }

    [Fact]
    public async Task IndependentBusinessEffectReplayProducesOneServerRowPerActivityExecution()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await Fixture.CreateAsync(
            SuccessProcess().Replace("maxRetries=\"0\"", "maxRetries=\"1\"", StringComparison.Ordinal), ct,
            configuration => configuration["ExternalTasks:Contracts:0:MaxAttempts"] = "2");
        await fixture.Runtime.StartAsync(fixture.Definition,
            new Dictionary<string, object>(StringComparer.Ordinal) { ["document"] = "synthetic" }, null, "a", "replay", ct);
        fixture.Db.ChangeTracker.Clear();
        var worker = Worker();
        var lease0 = Assert.Single(await fixture.Leases.ClaimAsync(worker,
            new ExternalTaskClaimCommand(["test.work"]), ct));

        var directory = Path.Combine(Path.GetTempPath(), "lc08-receipt", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var connectionString = $"Data Source={Path.Combine(directory, "effects.db")};Pooling=False";
            using var result = JsonDocument.Parse("{\"approved\":true}");
            await RecordIndependentEffectAsync(connectionString, $"external-task:{lease0.ActivityExecutionId:N}:primary", ct);

            // Reproduce the reclaim sequence from the verified spike at the real layers.
            var reclaimed = await ReclaimAsync(fixture, lease0, crashed: true, ct);
            await RecordIndependentEffectAsync(connectionString, $"external-task:{reclaimed.ActivityExecutionId:N}:primary", ct);
            var completion = new ExternalTaskCompleteCommand(reclaimed.LeaseId, reclaimed.LeaseGeneration,
                Guid.NewGuid(), result.RootElement.Clone());
            await fixture.Leases.CompleteAsync(worker, lease0.JobId, completion, ct);
            await fixture.Leases.CompleteAsync(worker, lease0.JobId, completion, ct);
            fixture.Db.ChangeTracker.Clear();
            await fixture.Runtime.ProcessExternalTaskContinuationsAsync(cancellationToken: ct);
            await fixture.Runtime.ProcessExternalTaskContinuationsAsync(cancellationToken: ct);
            fixture.Db.ChangeTracker.Clear();

            // Exactly one user task (the observable business effect) and one server receipt row.
            Assert.Single(await fixture.Db.Tasks.ToListAsync(ct));
            var receipt = Assert.Single(await fixture.Db.ExternalTaskEffectReceipts.ToListAsync(ct));
            Assert.Equal(lease0.JobId, receipt.JobId);
            await using var effects = new SqliteConnection(connectionString);
            await effects.OpenAsync(ct);
            await using var count = effects.CreateCommand();
            count.CommandText = "SELECT COUNT(*) FROM BusinessEffects";
            Assert.Equal(1L, await count.ExecuteScalarAsync(ct));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task MigrationAppliesCleanlyAndCreatesUniqueEffectKeyTable()
    {
        var ct = TestContext.Current.CancellationToken;
        var directory = Path.Combine(Path.GetTempPath(), "lc08-receipt-mig", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var dbFile = Path.Combine(directory, "migrated.db");
            var connectionString = $"Data Source={dbFile};Pooling=False";
            await using var connection = new SqliteConnection(connectionString);
            await connection.OpenAsync(ct);
            var options = new DbContextOptionsBuilder<BpmnDbContext>().UseSqlite(connection).Options;
            await using var db = new BpmnDbContext(options);
            await db.Database.MigrateAsync(ct);

            await using var count = connection.CreateCommand();
            count.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='ExternalTaskEffectReceipts';";
            Assert.Equal(1L, await count.ExecuteScalarAsync(ct));
            await using var unique = connection.CreateCommand();
            unique.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='index' AND name='IX_ExternalTaskEffectReceipts_EffectKey';";
            Assert.Equal(1L, await unique.ExecuteScalarAsync(ct));
            // The unique index must back exactly-once effect registration across generations.
            await using var ddl = connection.CreateCommand();
            ddl.CommandText = "SELECT sql FROM sqlite_master WHERE type='index' AND name='IX_ExternalTaskEffectReceipts_EffectKey';";
            var sql = (string?)await ddl.ExecuteScalarAsync(ct);
            Assert.Contains("UNIQUE", sql, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static async Task<ExternalTaskLease> ReclaimAsync(Fixture fixture, ExternalTaskLease lease, bool crashed, CancellationToken ct)
    {
        if (crashed)
        {
            var expiry = new FixedClock(DateTimeOffset.FromUnixTimeMilliseconds(lease.LeaseExpiresAt));
            await new ExternalTaskRecoveryService(fixture.Db, expiry).RecoverAsync(cancellationToken: ct);
        }
        else
        {
            await fixture.Leases.FailAsync(Worker(), lease.JobId,
                new ExternalTaskFailCommand(lease.LeaseId, lease.LeaseGeneration, Guid.NewGuid(),
                    "technical", "transport_failure"), ct);
        }
        fixture.Db.ChangeTracker.Clear();
        var retryAt = (await fixture.Db.ExternalTaskJobs.SingleAsync(ct)).AvailableAt;
        var retryClock = new FixedClock(DateTimeOffset.FromUnixTimeMilliseconds(retryAt));
        await new ExternalTaskRecoveryService(fixture.Db, retryClock).RecoverAsync(cancellationToken: ct);
        var configuration = VertexBPMN.Tests.Unit.Infrastructure.ExternalTaskContractResolverTests.Configuration();
        configuration["ExternalTasks:Contracts:0:MaxAttempts"] = "2";
        var service = new ExternalTaskLeaseService(fixture.Db,
            new ConfiguredExternalTaskContractResolver(configuration), retryClock);
        return Assert.Single(await service.ClaimAsync(Worker(), new ExternalTaskClaimCommand(["test.work"]), ct));
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private static async Task RecordIndependentEffectAsync(string connectionString, string effectKey, CancellationToken ct)
    {
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = "CREATE TABLE IF NOT EXISTS BusinessEffects (EffectKey TEXT PRIMARY KEY, Approved INTEGER NOT NULL); "
            + "INSERT INTO BusinessEffects (EffectKey, Approved) VALUES ($key, 1) ON CONFLICT(EffectKey) DO NOTHING;";
        command.Parameters.AddWithValue("$key", effectKey);
        await command.ExecuteNonQueryAsync(ct);
    }

    private static ExternalTaskWorkerContext Worker() => new("issuer", "worker", "a",
        new HashSet<string>(["test.work"], StringComparer.Ordinal), new HashSet<string>(StringComparer.Ordinal));

    private static string SuccessProcess()
    {
        return """
        <definitions xmlns="http://www.omg.org/spec/BPMN/20100524/MODEL" xmlns:vertex="https://vertexbpmn.io/schema/bpmn/1.0">
          <process id="p"><startEvent id="s"/><serviceTask id="work"><extensionElements>
            <vertex:externalTask topic="test.work" maxRetries="0" deadlineSeconds="300"/>
            <vertex:ioMapping><vertex:input name="text" expression="document"/><vertex:output name="result" target="review"/></vertex:ioMapping>
          </extensionElements></serviceTask><userTask id="after"/><endEvent id="e"/>
          <sequenceFlow id="f1" sourceRef="s" targetRef="work"/><sequenceFlow id="f2" sourceRef="work" targetRef="after"/>
          <sequenceFlow id="f3" sourceRef="after" targetRef="e"/></process></definitions>
        """;
    }

    private sealed class Fixture(SqliteConnection connection, BpmnDbContext db, ProcessDefinition definition,
        PersistentProcessExecutionRuntime runtime, ExternalTaskLeaseService leases) : IAsyncDisposable
    {
        public BpmnDbContext Db => db;
        public ProcessDefinition Definition => definition;
        public PersistentProcessExecutionRuntime Runtime => runtime;
        public ExternalTaskLeaseService Leases => leases;

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000", Justification = "The returned async-disposable Fixture owns both the connection and context; construction failures dispose both before rethrowing.")]
        public static async Task<Fixture> CreateAsync(string xml, CancellationToken ct,
            Action<Microsoft.Extensions.Configuration.IConfigurationRoot>? configure = null)
        {
            var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
            BpmnDbContext? db = null;
            try
            {
                await connection.OpenAsync(ct);
                db = new BpmnDbContext(new DbContextOptionsBuilder<BpmnDbContext>().UseSqlite(connection).Options);
                await db.Database.EnsureCreatedAsync(ct);
                var deployment = new EngineDeployment { Id = Guid.NewGuid(), Name = "receipt", TenantId = "a" };
                var definition = new ProcessDefinition
                {
                    Id = Guid.NewGuid(),
                    DeploymentId = deployment.Id,
                    Key = "p",
                    Name = "receipt",
                    TenantId = "a",
                    TenantScope = "a",
                    Version = 1,
                    BpmnXml = xml
                };
                db.AddRange(deployment, definition);
                await db.SaveChangesAsync(ct);
                var configuration = VertexBPMN.Tests.Unit.Infrastructure.ExternalTaskContractResolverTests.Configuration();
                configure?.Invoke(configuration);
                var contracts = new ConfiguredExternalTaskContractResolver(configuration);
                var runtime = new PersistentProcessExecutionRuntime(db, Mock.Of<IServiceTaskRegistry>(),
                    Mock.Of<IDecisionService>(), NullLogger<PersistentProcessExecutionRuntime>.Instance,
                    configuration, contracts);
                return new Fixture(connection, db, definition, runtime,
                    new ExternalTaskLeaseService(db, contracts, configuration: configuration));
            }
            catch
            {
                if (db is not null)
                {
                    await db.DisposeAsync();
                }
                await connection.DisposeAsync();
                throw;
            }
        }

        public async ValueTask DisposeAsync()
        {
            await db.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}
