using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Infrastructure.Persistence;

namespace VertexBPMN.Tests.Unit.Infrastructure;

public sealed class ExternalTaskLeaseServiceTests
{
    [Fact]
    public async Task ClaimCreatesFencedAttemptAndHeartbeatRequiresCurrentWorkerLease()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await Fixture.CreateAsync(ct);
        var service = fixture.Service();
        var worker = Worker("worker-a");
        var lease = Assert.Single(await service.ClaimAsync(worker,
            new ExternalTaskClaimCommand(["test.work"], 1, 60), ct));
        Assert.Equal(fixture.JobId, lease.JobId);
        Assert.NotEqual(Guid.Empty, lease.LeaseId);
        Assert.Equal(1, lease.LeaseGeneration);
        Assert.Equal(1, lease.AttemptNumber);
        Assert.Equal("synthetic", lease.Input.GetProperty("text").GetString());
        fixture.Db.ChangeTracker.Clear();
        var stored = await fixture.Db.ExternalTaskJobs.SingleAsync(ct);
        Assert.Equal(ExternalTaskState.Leased, stored.State);
        Assert.Equal("issuer", stored.WorkerIssuer);
        Assert.Equal("worker-a", stored.WorkerSubject);
        var attempt = await fixture.Db.ExternalTaskAttempts.SingleAsync(ct);
        Assert.Equal(lease.LeaseId, attempt.LeaseId);
        Assert.Equal(1, attempt.LeaseGeneration);

        fixture.Clock.Advance(TimeSpan.FromSeconds(20));
        var heartbeat = await service.HeartbeatAsync(worker, lease.JobId,
            new ExternalTaskHeartbeatCommand(lease.LeaseId, lease.LeaseGeneration, 60), ct);
        Assert.Equal(fixture.Clock.GetUtcNow().ToUnixTimeMilliseconds(), heartbeat.ServerTime);
        Assert.Equal(heartbeat.ServerTime + 60_000, heartbeat.LeaseExpiresAt);
        var hidden = await Assert.ThrowsAsync<ExternalTaskLeaseException>(async () =>
            await service.HeartbeatAsync(Worker("worker-b"), lease.JobId,
                new ExternalTaskHeartbeatCommand(lease.LeaseId, lease.LeaseGeneration, 60), ct));
        Assert.Equal("external_task_not_found", hidden.Code);
    }

    [Fact]
    public async Task ClaimIsSingleWinnerAndPolicyRevocationBlocksEveryLaterOperation()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await Fixture.CreateAsync(ct);
        var first = Assert.Single(await fixture.Service().ClaimAsync(Worker("worker-a"),
            new ExternalTaskClaimCommand(["test.work"]), ct));
        fixture.Db.ChangeTracker.Clear();
        Assert.Empty(await fixture.Service().ClaimAsync(Worker("worker-b"),
            new ExternalTaskClaimCommand(["test.work"]), ct));
        fixture.Configuration["ExternalTasks:Contracts:0:Enabled"] = "false";
        var revoked = await Assert.ThrowsAsync<ExternalTaskLeaseException>(async () =>
            await fixture.Service().HeartbeatAsync(Worker("worker-a"), first.JobId,
                new ExternalTaskHeartbeatCommand(first.LeaseId, first.LeaseGeneration), ct));
        Assert.Equal("policy_revoked", revoked.Code);
        fixture.Db.ChangeTracker.Clear();
        Assert.Equal(first.LeaseId, (await fixture.Db.ExternalTaskJobs.SingleAsync(ct)).LeaseId);
        Assert.Single(await fixture.Db.ExternalTaskAttempts.ToListAsync(ct));
    }

    [Fact]
    public async Task StatusAndAttemptHistoryAreVisibleOnlyToBoundAuthorizedWorker()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await Fixture.CreateAsync(ct);
        var service = fixture.Service();
        var worker = Worker("worker-a");
        var lease = Assert.Single(await service.ClaimAsync(worker,
            new ExternalTaskClaimCommand(["test.work"]), ct));
        fixture.Db.ChangeTracker.Clear();

        var status = await service.GetAsync(worker, lease.JobId, ct);
        Assert.Equal("Leased", status.State);
        Assert.Equal(1, status.LeaseGeneration);
        var attempts = await service.GetAttemptsAsync(worker, lease.JobId, null, 50, ct);
        var attempt = Assert.Single(attempts.Items);
        Assert.Equal(1, attempt.AttemptNumber);
        Assert.Null(attempts.NextCursor);

        var hidden = await Assert.ThrowsAsync<ExternalTaskLeaseException>(async () =>
            await service.GetAsync(Worker("worker-b"), lease.JobId, ct));
        Assert.Equal("external_task_not_found", hidden.Code);
        var invalidCursor = await Assert.ThrowsAsync<ExternalTaskLeaseException>(async () =>
            await service.GetAttemptsAsync(worker, lease.JobId, "not-a-cursor", 50, ct));
        Assert.Equal("invalid_limits", invalidCursor.Code);
    }

    [Fact]
    public async Task ExpiredLeaseIsReclaimedWithNewFenceAndOldWorkerCannotMutateIt()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await Fixture.CreateAsync(ct, maxAttempts: 2);
        var service = fixture.Service();
        var first = Assert.Single(await service.ClaimAsync(Worker("worker-a"),
            new ExternalTaskClaimCommand(["test.work"], 1, 60), ct));
        fixture.Clock.Advance(TimeSpan.FromSeconds(60));
        fixture.Db.ChangeTracker.Clear();

        var second = Assert.Single(await service.ClaimAsync(Worker("worker-b"),
            new ExternalTaskClaimCommand(["test.work"], 1, 60), ct));

        Assert.NotEqual(first.LeaseId, second.LeaseId);
        Assert.Equal(2, second.LeaseGeneration);
        Assert.Equal(2, second.AttemptNumber);
        fixture.Db.ChangeTracker.Clear();
        var attempts = await fixture.Db.ExternalTaskAttempts.OrderBy(item => item.AttemptNumber).ToListAsync(ct);
        Assert.Equal("lease_expired", attempts[0].EndReason);
        Assert.Null(attempts[1].EndedAt);
        var stale = await Assert.ThrowsAsync<ExternalTaskLeaseException>(async () =>
            await service.HeartbeatAsync(Worker("worker-a"), first.JobId,
                new ExternalTaskHeartbeatCommand(first.LeaseId, first.LeaseGeneration), ct));
        Assert.Equal("external_task_not_found", stale.Code);
        Assert.Equal(second.LeaseId, (await fixture.Db.ExternalTaskJobs.AsNoTracking().SingleAsync(ct)).LeaseId);
    }

    [Fact]
    public async Task ServerSideWorkerBlocklistAppliesImmediatelyToEveryOperation()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await Fixture.CreateAsync(ct);
        var service = fixture.Service();
        var worker = Worker("worker-a");
        var lease = Assert.Single(await service.ClaimAsync(worker,
            new ExternalTaskClaimCommand(["test.work"]), ct));
        fixture.Configuration["ExternalTasks:BlockedWorkers:0:Issuer"] = "issuer";
        fixture.Configuration["ExternalTasks:BlockedWorkers:0:Subject"] = "worker-a";

        var heartbeat = await Assert.ThrowsAsync<ExternalTaskLeaseException>(async () =>
            await service.HeartbeatAsync(worker, lease.JobId,
                new ExternalTaskHeartbeatCommand(lease.LeaseId, lease.LeaseGeneration), ct));
        Assert.Equal("worker_forbidden", heartbeat.Code);
        var read = await Assert.ThrowsAsync<ExternalTaskLeaseException>(async () =>
            await service.GetAsync(worker, lease.JobId, ct));
        Assert.Equal("worker_forbidden", read.Code);
        var claim = await Assert.ThrowsAsync<ExternalTaskLeaseException>(async () =>
            await service.ClaimAsync(worker, new ExternalTaskClaimCommand(["test.work"]), ct));
        Assert.Equal("worker_forbidden", claim.Code);
    }

    [Theory]
    [InlineData("topic", "topic_forbidden")]
    [InlineData("batch", "invalid_limits")]
    [InlineData("duration", "invalid_limits")]
    [InlineData("identity", "worker_forbidden")]
    public async Task ClaimRejectsInvalidAuthorityAndBoundsWithoutMutation(string kind, string code)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await Fixture.CreateAsync(ct);
        var worker = kind == "identity" ? Worker("") : Worker("worker");
        var command = kind switch
        {
            "topic" => new ExternalTaskClaimCommand(["other"]),
            "batch" => new ExternalTaskClaimCommand(["test.work"], 11),
            "duration" => new ExternalTaskClaimCommand(["test.work"], 1, 9),
            _ => new ExternalTaskClaimCommand(["test.work"])
        };
        var error = await Assert.ThrowsAsync<ExternalTaskLeaseException>(async () =>
            await fixture.Service().ClaimAsync(worker, command, ct));
        Assert.Equal(code, error.Code);
        fixture.Db.ChangeTracker.Clear();
        Assert.Equal(ExternalTaskState.Ready, (await fixture.Db.ExternalTaskJobs.SingleAsync(ct)).State);
        Assert.Empty(await fixture.Db.ExternalTaskAttempts.ToListAsync(ct));
    }

    private static ExternalTaskWorkerContext Worker(string subject) => new(
        "issuer", subject, "a", new HashSet<string>(["test.work"], StringComparer.Ordinal),
        new HashSet<string>(StringComparer.Ordinal));

    private sealed class Fixture(SqliteConnection connection, BpmnDbContext db,
        Microsoft.Extensions.Configuration.IConfigurationRoot configuration, ManualTimeProvider clock, Guid jobId)
        : IAsyncDisposable
    {
        public BpmnDbContext Db => db;
        public Microsoft.Extensions.Configuration.IConfigurationRoot Configuration => configuration;
        public ManualTimeProvider Clock => clock;
        public Guid JobId => jobId;
        public ExternalTaskLeaseService Service() => new(db, new ConfiguredExternalTaskContractResolver(configuration), clock, configuration);

        public static async Task<Fixture> CreateAsync(CancellationToken ct, int maxAttempts = 1)
        {
            var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
            await connection.OpenAsync(ct);
            var db = new BpmnDbContext(new DbContextOptionsBuilder<BpmnDbContext>().UseSqlite(connection).Options);
            await db.Database.EnsureCreatedAsync(ct);
            var deployment = new EngineDeployment { Id = Guid.NewGuid(), Name = "lease", TenantId = "a" };
            var definition = new ProcessDefinition { Id = Guid.NewGuid(), Key = "lease", Name = "lease", TenantId = "a", TenantScope = "a", Version = 1, DeploymentId = deployment.Id };
            var process = new ProcessInstance { Id = Guid.NewGuid(), ProcessDefinitionId = definition.Id, TenantId = "a", Status = ProcessInstanceStatus.Running, Revision = 1 };
            var executionId = Guid.NewGuid();
            var wait = new ExecutionToken { Id = Guid.NewGuid(), ProcessInstanceId = process.Id, CurrentNodeId = "work", NodeType = "serviceTask", State = ExecutionToken.WaitingState, ActivityExecutionId = executionId, ScopeExecutionId = process.Id, Revision = 1 };
            var clock = new ManualTimeProvider(DateTimeOffset.FromUnixTimeMilliseconds(1_000_000));
            var job = new ExternalTaskJob
            {
                Id = Guid.NewGuid(), TenantId = "a", ProcessInstanceId = process.Id, DefinitionId = definition.Id,
                DefinitionVersion = 1, ActivityId = "work", ActivityExecutionId = executionId, WaitTokenId = wait.Id,
                ScopeExecutionId = process.Id, Topic = "test.work", ContractVersion = "v1",
                InputSnapshot = "{\"text\":\"synthetic\"}", SchemaSnapshot = "{\"dialect\":\"vertex.scalar-contract.v1\"}",
                State = ExternalTaskState.Ready, Revision = 1, CreatedAt = 1_000_000, AvailableAt = 1_000_000,
                Deadline = 1_300_000, MaxAttempts = maxAttempts
            };
            db.AddRange(deployment, definition, process, wait, job);
            await db.SaveChangesAsync(ct);
            var configuration = ExternalTaskContractResolverTests.Configuration();
            configuration["ExternalTasks:Contracts:0:MaxAttempts"] = maxAttempts.ToString(System.Globalization.CultureInfo.InvariantCulture);
            return new Fixture(connection, db, configuration, clock, job.Id);
        }

        public async ValueTask DisposeAsync()
        {
            await db.DisposeAsync();
            await connection.DisposeAsync();
        }
    }

    private sealed class ManualTimeProvider(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan duration) => _now += duration;
    }
}
