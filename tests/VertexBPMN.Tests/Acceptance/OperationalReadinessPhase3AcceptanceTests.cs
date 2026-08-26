using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging.Abstractions;
using VertexBPMN.Api.Health;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Infrastructure;
using VertexBPMN.Infrastructure.Messaging;
using VertexBPMN.Infrastructure.Operational;
using VertexBPMN.Infrastructure.Persistence;
using VertexBPMN.Infrastructure.Persistence.Services;

namespace VertexBPMN.Tests.Acceptance;

[Trait("Category", "Phase3Acceptance")]
public sealed class OperationalReadinessPhase3AcceptanceTests : IDisposable
{
    private readonly List<string> _databaseFiles = [];

    [Fact]
    public async Task P3_AC_01_Two_publishers_lease_each_outbox_message_exactly_once()
    {
        await using var provider = await CreateRuntimeProviderAsync();
        var transport = new RecordingTransport();
        var options = Options(batchSize: 100);
        var publisherA = Publisher(provider, transport, options);
        var publisherB = Publisher(provider, transport, options);

        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BpmnDbContext>();
            db.RuntimeOutbox.AddRange(Enumerable.Range(0, 40).Select(index => Message(index)));
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await Task.WhenAll(
            publisherA.RunOnceAsync(TestContext.Current.CancellationToken),
            publisherB.RunOnceAsync(TestContext.Current.CancellationToken));

        await using var verificationScope = provider.CreateAsyncScope();
        var verificationDb = verificationScope.ServiceProvider.GetRequiredService<BpmnDbContext>();
        var messages = await verificationDb.RuntimeOutbox.AsNoTracking().ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(40, messages.Count);
        Assert.All(messages, message => Assert.Equal("Published", message.State));
        Assert.All(messages, message => Assert.Equal(1, transport.Deliveries[message.Id]));
    }

    [Fact]
    public async Task P3_AC_02_Broker_failure_retries_and_preserves_diagnostic_until_recovery()
    {
        await using var provider = await CreateRuntimeProviderAsync();
        var transport = new RecordingTransport(failuresBeforeSuccess: 1);
        var options = Options(batchSize: 1, retryDelaySeconds: 0);
        var publisher = Publisher(provider, transport, options);
        var message = Message(1);
        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BpmnDbContext>();
            db.RuntimeOutbox.Add(message);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        Assert.Equal(0, await publisher.RunOnceAsync(TestContext.Current.CancellationToken));
        await using (var failedScope = provider.CreateAsyncScope())
        {
            var failed = await failedScope.ServiceProvider.GetRequiredService<BpmnDbContext>()
                .RuntimeOutbox.AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
            Assert.Equal("Pending", failed.State);
            Assert.Equal(1, failed.Attempts);
            Assert.Contains("simulated broker outage", failed.LastError, StringComparison.OrdinalIgnoreCase);
        }

        Assert.Equal(1, await publisher.RunOnceAsync(TestContext.Current.CancellationToken));
        await using var recoveredScope = provider.CreateAsyncScope();
        var recovered = await recoveredScope.ServiceProvider.GetRequiredService<BpmnDbContext>()
            .RuntimeOutbox.AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal("Published", recovered.State);
        Assert.Equal(2, recovered.Attempts);
        Assert.Null(recovered.LastError);
    }

    [Fact]
    public async Task P3_AC_03_Readiness_fails_when_broker_is_unavailable()
    {
        await using var provider = CreateInMemoryOperationalProvider();
        var check = new OperationalReadinessHealthCheck(
            provider,
            new RecordingTransport(isHealthy: false),
            Options(enabled: true));

        var result = await check.CheckHealthAsync(HealthContext(), TestContext.Current.CancellationToken);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Contains("broker", result.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task P3_AC_04_Readiness_fails_when_schema_has_pending_migrations()
    {
        var bpmnFile = NewDatabaseFile();
        var services = new ServiceCollection();
        services.AddDbContext<BpmnDbContext>(options => options.UseSqlite($"Data Source={bpmnFile}"));
        AddOtherInMemoryContexts(services);
        await using var provider = services.BuildServiceProvider();
        await using (var setupScope = provider.CreateAsyncScope())
        {
            await setupScope.ServiceProvider.GetRequiredService<BpmnDbContext>()
                .Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        }
        var check = new OperationalReadinessHealthCheck(
            provider,
            new RecordingTransport(),
            Options(enabled: true));

        var result = await check.CheckHealthAsync(HealthContext(), TestContext.Current.CancellationToken);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Contains("pending migration", result.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task P3_AC_05_Runtime_metrics_are_read_from_persistent_state()
    {
        await using var provider = await CreateRuntimeProviderAsync();
        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BpmnDbContext>();
            db.RuntimeOutbox.AddRange(Message(1), Message(2));
            db.WorkerRegistrations.Add(new WorkerRegistration
            {
                Id = "worker-a",
                HostName = "pod-a",
                RegisteredAt = DateTime.UtcNow,
                LastHeartbeat = DateTime.UtcNow,
                MaxCapacity = 4
            });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var metricsScope = provider.CreateAsyncScope();
        var metrics = await metricsScope.ServiceProvider.GetRequiredService<IRuntimeMetricsReader>()
            .ReadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, metrics["outbox_pending"]);
        Assert.Equal(1, metrics["workers_active"]);
        Assert.Equal(0, metrics["process_instances_total"]);
        Assert.Equal(0, metrics["jobs_total"]);
    }

    [Fact]
    public void P3_AC_06_Stage_configuration_and_kubernetes_manifest_are_hardened()
    {
        var root = RepositoryRoot();
        using var stage = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            root, "src", "VertexBPMN.Api", "appsettings.Stage.json")));
        Assert.Equal("Stage", stage.RootElement.GetProperty("OperationalMode").GetString());

        var manifests = new[]
        {
            "k8s-prerequisites.yaml",
            "k8s-migration-job.yaml",
            "k8s-deployment.yaml"
        };
        var manifest = string.Join('\n', manifests.Select(file => File.ReadAllText(Path.Combine(root, file))));
        Assert.DoesNotContain(":latest", manifest, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Password=", manifest, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secretRef:", manifest, StringComparison.Ordinal);
        Assert.Contains("readOnlyRootFilesystem: true", manifest, StringComparison.Ordinal);
        Assert.Contains("runAsNonRoot: true", manifest, StringComparison.Ordinal);
        Assert.Contains("kind: PodDisruptionBudget", manifest, StringComparison.Ordinal);
        Assert.Contains("--migrate-only", manifest, StringComparison.Ordinal);
        Assert.Contains("/api/ready", manifest, StringComparison.Ordinal);
        Assert.DoesNotContain("kind: Job", File.ReadAllText(Path.Combine(root, "k8s-deployment.yaml")),
            StringComparison.Ordinal);
    }

    [Fact]
    public void P3_AC_07_Production_requires_an_external_outbox_transport()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["OperationalMode"] = "Production",
            ["ConnectionStrings:DependencyRegistry"] = "Data Source=dependency-registry.db",
            ["DataProtection:KeyRingPath"] = Path.Combine(Path.GetTempPath(), "vertexbpmn-phase3-keys")
        }).Build();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            new ServiceCollection().AddBpmnPersistenceServices(configuration));

        Assert.Contains("Outbox", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void P3_AC_09_Migrations_generate_provider_native_relational_types()
    {
        using var postgresBpmn = new BpmnDbContext(new DbContextOptionsBuilder<BpmnDbContext>()
            .UseNpgsql("Host=localhost;Database=phase3;Username=phase3;Password=phase3").Options);
        using var postgresMining = new ProcessMiningEventDbContext(
            new DbContextOptionsBuilder<ProcessMiningEventDbContext>()
                .UseNpgsql("Host=localhost;Database=phase3;Username=phase3;Password=phase3").Options);
        var postgres = MigrationScript(postgresBpmn);
        var postgresIdentity = MigrationScript(postgresMining);
        Assert.Contains("uuid", postgres, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("boolean", postgres, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("timestamp with time zone", postgres, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("GENERATED BY DEFAULT AS IDENTITY", postgresIdentity, StringComparison.OrdinalIgnoreCase);

        using var sqlServerBpmn = new BpmnDbContext(new DbContextOptionsBuilder<BpmnDbContext>()
            .UseSqlServer("Server=localhost;Database=phase3;User Id=phase3;Password=phase3;TrustServerCertificate=true").Options);
        using var sqlServerMining = new ProcessMiningEventDbContext(
            new DbContextOptionsBuilder<ProcessMiningEventDbContext>()
                .UseSqlServer("Server=localhost;Database=phase3;User Id=phase3;Password=phase3;TrustServerCertificate=true").Options);
        var sqlServer = MigrationScript(sqlServerBpmn);
        var sqlServerIdentity = MigrationScript(sqlServerMining);
        Assert.Contains("uniqueidentifier", sqlServer, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("bit", sqlServer, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("datetime2", sqlServer, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("IDENTITY", sqlServerIdentity, StringComparison.OrdinalIgnoreCase);
    }

    private static string MigrationScript(DbContext context) =>
        context.GetService<IMigrator>().GenerateScript();

    private async Task<ServiceProvider> CreateRuntimeProviderAsync()
    {
        var databaseFile = NewDatabaseFile();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<BpmnDbContext>(options => options.UseSqlite($"Data Source={databaseFile}"));
        services.AddScoped<IRuntimeMetricsReader, RuntimeMetricsReader>();
        var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<BpmnDbContext>().Database.EnsureCreatedAsync(
            TestContext.Current.CancellationToken);
        return provider;
    }

    private static ServiceProvider CreateInMemoryOperationalProvider()
    {
        var services = new ServiceCollection();
        services.AddDbContext<BpmnDbContext>(options => options.UseInMemoryDatabase(Guid.NewGuid().ToString("N")));
        AddOtherInMemoryContexts(services);
        return services.BuildServiceProvider();
    }

    private static void AddOtherInMemoryContexts(IServiceCollection services)
    {
        services.AddDbContext<TenantDbContext>(options => options.UseInMemoryDatabase(Guid.NewGuid().ToString("N")));
        services.AddDbContext<SimulationScenarioDbContext>(options => options.UseInMemoryDatabase(Guid.NewGuid().ToString("N")));
        services.AddDbContext<ProcessMiningEventDbContext>(options => options.UseInMemoryDatabase(Guid.NewGuid().ToString("N")));
        services.AddDbContext<DecisionDbContext>(options => options.UseInMemoryDatabase(Guid.NewGuid().ToString("N")));
        services.AddDbContext<DependencyRegistryDbContext>(options => options.UseInMemoryDatabase(Guid.NewGuid().ToString("N")));
    }

    private static RuntimeOutboxPublisherService Publisher(
        ServiceProvider provider,
        IRuntimeOutboxTransport transport,
        RuntimeOutboxOptions options) =>
        new(provider.GetRequiredService<IServiceScopeFactory>(), transport, options,
            NullLogger<RuntimeOutboxPublisherService>.Instance);

    private static RuntimeOutboxOptions Options(
        bool enabled = true,
        int batchSize = 50,
        int retryDelaySeconds = 0) => new()
    {
        Enabled = enabled,
        Provider = "Test",
        ConnectionString = "test",
        Destination = "phase3-test",
        BatchSize = batchSize,
        RetryDelaySeconds = retryDelaySeconds,
        LeaseSeconds = 5,
        MaxAttempts = 3
    };

    private static RuntimeOutboxMessage Message(int index) => new()
    {
        Id = Guid.NewGuid(),
        EventType = "Phase3Acceptance",
        Payload = JsonSerializer.Serialize(new { index }),
        State = "Pending",
        OccurredAt = DateTime.UtcNow.AddMilliseconds(index)
    };

    private static HealthCheckContext HealthContext() => new()
    {
        Registration = new HealthCheckRegistration(
            "operational_readiness",
            _ => throw new NotSupportedException(),
            HealthStatus.Unhealthy,
            ["ready"])
    };

    private string NewDatabaseFile()
    {
        var path = Path.Combine(Path.GetTempPath(), $"vertexbpmn-phase3-{Guid.NewGuid():N}.sqlite");
        _databaseFiles.Add(path);
        return path;
    }

    private static string RepositoryRoot() =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));

    public void Dispose()
    {
        foreach (var path in _databaseFiles.Where(File.Exists))
            File.Delete(path);
    }

    private sealed class RecordingTransport(
        int failuresBeforeSuccess = 0,
        bool isHealthy = true) : IRuntimeOutboxTransport
    {
        private int _remainingFailures = failuresBeforeSuccess;
        public ConcurrentDictionary<Guid, int> Deliveries { get; } = new();

        public ValueTask PublishAsync(RuntimeOutboxMessage message, CancellationToken cancellationToken = default)
        {
            if (Interlocked.Decrement(ref _remainingFailures) >= 0)
                return ValueTask.FromException(new InvalidOperationException("simulated broker outage"));
            Deliveries.AddOrUpdate(message.Id, 1, (_, count) => count + 1);
            return ValueTask.CompletedTask;
        }

        public ValueTask<OutboxTransportHealth> CheckHealthAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(new OutboxTransportHealth(isHealthy, isHealthy ? "broker ready" : "broker unavailable"));
    }
}
