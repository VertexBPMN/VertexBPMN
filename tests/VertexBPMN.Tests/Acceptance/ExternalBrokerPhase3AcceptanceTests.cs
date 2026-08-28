using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Infrastructure.Messaging;
using VertexBPMN.Infrastructure.Persistence;
using VertexBPMN.Infrastructure.Persistence.Services;

namespace VertexBPMN.Tests.Acceptance;

public sealed class ExternalBrokerPhase3AcceptanceTests
{
    [Fact]
    [Trait("Category", "Phase3ExternalAcceptance")]
    public async Task P3_EXT_01_RabbitMq_health_publish_and_consume_roundtrip()
    {
        var connectionString = Environment.GetEnvironmentVariable("VERTEXBPMN_TEST_RABBITMQ");
        Assert.False(string.IsNullOrWhiteSpace(connectionString),
            "VERTEXBPMN_TEST_RABBITMQ must point to the CI RabbitMQ service.");

        var destination = $"vertexbpmn-phase3-{Guid.NewGuid():N}";
        var options = new RuntimeOutboxOptions
        {
            Enabled = true,
            Provider = "RabbitMq",
            ConnectionString = connectionString,
            Destination = destination
        };
        var transport = new RabbitMqRuntimeOutboxTransport(options);
        var factory = new ConnectionFactory { Uri = new Uri(connectionString) };
        await using var connection = await factory.CreateConnectionAsync(TestContext.Current.CancellationToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: TestContext.Current.CancellationToken);
        await channel.ExchangeDeclareAsync(
            destination,
            ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            cancellationToken: TestContext.Current.CancellationToken);
        var queue = await channel.QueueDeclareAsync(
            queue: string.Empty,
            durable: false,
            exclusive: true,
            autoDelete: true,
            arguments: null,
            cancellationToken: TestContext.Current.CancellationToken);
        await channel.QueueBindAsync(
            queue.QueueName,
            destination,
            "#",
            arguments: null,
            cancellationToken: TestContext.Current.CancellationToken);

        var health = await transport.CheckHealthAsync(TestContext.Current.CancellationToken);
        Assert.True(health.IsHealthy, health.Description);
        var message = new RuntimeOutboxMessage
        {
            Id = Guid.NewGuid(),
            ProcessInstanceId = Guid.NewGuid(),
            EventType = "Phase3ExternalRoundtrip",
            Payload = JsonSerializer.Serialize(new { value = 42 }),
            State = "InFlight",
            OccurredAt = DateTime.UtcNow
        };
        await transport.PublishAsync(message, TestContext.Current.CancellationToken);

        var delivered = await channel.BasicGetAsync(
            queue.QueueName,
            autoAck: true,
            TestContext.Current.CancellationToken);
        Assert.NotNull(delivered);
        Assert.Equal(message.Id.ToString("N"), delivered.BasicProperties.MessageId);
        var envelope = Encoding.UTF8.GetString(delivered.Body.ToArray());
        Assert.Contains(message.EventType, envelope, StringComparison.Ordinal);
        Assert.Contains("42", envelope, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "Phase3ExternalAcceptance")]
    public async Task P3_EXT_02_All_EF_migrations_apply_to_real_PostgreSql_databases()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable("VERTEXBPMN_TEST_POSTGRES_ADMIN");
        Assert.False(string.IsNullOrWhiteSpace(adminConnectionString),
            "VERTEXBPMN_TEST_POSTGRES_ADMIN must point to the CI PostgreSQL service.");

        var databaseNames = new[]
        {
            $"p3_bpmn_{Guid.NewGuid():N}",
            $"p3_tenants_{Guid.NewGuid():N}",
            $"p3_simulation_{Guid.NewGuid():N}",
            $"p3_mining_{Guid.NewGuid():N}",
            $"p3_decision_{Guid.NewGuid():N}"
        };

        await using var admin = new NpgsqlConnection(adminConnectionString);
        await admin.OpenAsync(TestContext.Current.CancellationToken);
        try
        {
            foreach (var databaseName in databaseNames)
                await ExecuteAdminCommandAsync(admin, $"CREATE DATABASE \"{databaseName}\"");

            await AssertMigrationsCurrentAsync(new BpmnDbContext(
                new DbContextOptionsBuilder<BpmnDbContext>()
                    .UseVertexNpgsql(ConnectionStringFor(adminConnectionString, databaseNames[0])).Options));
            await AssertMigrationsCurrentAsync(new TenantDbContext(
                new DbContextOptionsBuilder<TenantDbContext>()
                    .UseVertexNpgsql(ConnectionStringFor(adminConnectionString, databaseNames[1])).Options));
            await AssertMigrationsCurrentAsync(new SimulationScenarioDbContext(
                new DbContextOptionsBuilder<SimulationScenarioDbContext>()
                    .UseVertexNpgsql(ConnectionStringFor(adminConnectionString, databaseNames[2])).Options));
            await AssertMigrationsCurrentAsync(new ProcessMiningEventDbContext(
                new DbContextOptionsBuilder<ProcessMiningEventDbContext>()
                    .UseVertexNpgsql(ConnectionStringFor(adminConnectionString, databaseNames[3])).Options));
            await AssertMigrationsCurrentAsync(new DecisionDbContext(
                new DbContextOptionsBuilder<DecisionDbContext>()
                    .UseVertexNpgsql(ConnectionStringFor(adminConnectionString, databaseNames[4])).Options));
        }
        finally
        {
            foreach (var databaseName in databaseNames)
                await ExecuteAdminCommandAsync(admin, $"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE)");
        }
    }

    [Fact]
    [Trait("Category", "Phase3ExternalAcceptance")]
    public async Task P3_EXT_03_Two_isolated_publishers_share_PostgreSql_without_duplicate_leases()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable("VERTEXBPMN_TEST_POSTGRES_ADMIN");
        Assert.False(string.IsNullOrWhiteSpace(adminConnectionString),
            "VERTEXBPMN_TEST_POSTGRES_ADMIN must point to the CI PostgreSQL service.");

        var databaseName = $"p3_multipod_{Guid.NewGuid():N}";
        await using var admin = new NpgsqlConnection(adminConnectionString);
        await admin.OpenAsync(TestContext.Current.CancellationToken);
        await ExecuteAdminCommandAsync(admin, $"CREATE DATABASE \"{databaseName}\"");
        try
        {
            var connectionString = ConnectionStringFor(adminConnectionString, databaseName);
            await using (var migrationContext = new BpmnDbContext(
                             new DbContextOptionsBuilder<BpmnDbContext>()
                                 .UseVertexNpgsql(connectionString).Options))
            {
                await migrationContext.Database.MigrateAsync(TestContext.Current.CancellationToken);
                migrationContext.RuntimeOutbox.AddRange(Enumerable.Range(0, 40).Select(index =>
                    new RuntimeOutboxMessage
                    {
                        Id = Guid.NewGuid(),
                        EventType = "Phase3MultiPod",
                        Payload = JsonSerializer.Serialize(new { index }),
                        State = "Pending",
                        OccurredAt = DateTime.UtcNow.AddMilliseconds(index)
                    }));
                await migrationContext.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            await using var podA = RuntimePublisherProvider(connectionString);
            await using var podB = RuntimePublisherProvider(connectionString);
            var transport = new ConcurrentRecordingTransport();
            var options = new RuntimeOutboxOptions
            {
                Enabled = true,
                Provider = "Test",
                ConnectionString = "test",
                Destination = "phase3-multipod",
                BatchSize = 40,
                LeaseSeconds = 30,
                RetryDelaySeconds = 0,
                MaxAttempts = 3
            };
            var publisherA = new RuntimeOutboxPublisherService(
                podA.GetRequiredService<IServiceScopeFactory>(), transport, options,
                NullLogger<RuntimeOutboxPublisherService>.Instance);
            var publisherB = new RuntimeOutboxPublisherService(
                podB.GetRequiredService<IServiceScopeFactory>(), transport, options,
                NullLogger<RuntimeOutboxPublisherService>.Instance);

            await Task.WhenAll(
                publisherA.RunOnceAsync(TestContext.Current.CancellationToken),
                publisherB.RunOnceAsync(TestContext.Current.CancellationToken));

            await using var verification = new BpmnDbContext(
                new DbContextOptionsBuilder<BpmnDbContext>().UseNpgsql(connectionString).Options);
            var messages = await verification.RuntimeOutbox.AsNoTracking()
                .ToListAsync(TestContext.Current.CancellationToken);
            Assert.Equal(40, messages.Count);
            Assert.All(messages, message => Assert.Equal("Published", message.State));
            Assert.All(messages, message => Assert.Equal(1, transport.Deliveries[message.Id]));
        }
        finally
        {
            await ExecuteAdminCommandAsync(admin, $"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE)");
        }
    }

    [Fact]
    [Trait("Category", "Phase3ExternalAcceptance")]
    public async Task P3_EXT_04_RabbitMq_rejects_unroutable_mandatory_delivery()
    {
        var connectionString = Environment.GetEnvironmentVariable("VERTEXBPMN_TEST_RABBITMQ");
        Assert.False(string.IsNullOrWhiteSpace(connectionString),
            "VERTEXBPMN_TEST_RABBITMQ must point to the CI RabbitMQ service.");

        var transport = new RabbitMqRuntimeOutboxTransport(new RuntimeOutboxOptions
        {
            Enabled = true,
            Provider = "RabbitMq",
            ConnectionString = connectionString,
            Destination = $"vertexbpmn-unroutable-{Guid.NewGuid():N}"
        });
        var message = new RuntimeOutboxMessage
        {
            Id = Guid.NewGuid(),
            EventType = "NoBoundQueue",
            Payload = "{}",
            State = "InFlight",
            OccurredAt = DateTime.UtcNow
        };

        var exception = await Assert.ThrowsAsync<PublishReturnException>(async () =>
            await transport.PublishAsync(message, TestContext.Current.CancellationToken));
        Assert.Equal(Constants.NoRoute, exception.ReplyCode);
    }

    private static async Task AssertMigrationsCurrentAsync(DbContext context)
    {
        await using (context)
        {
            await context.Database.MigrateAsync(TestContext.Current.CancellationToken);
            var pending = await context.Database.GetPendingMigrationsAsync(TestContext.Current.CancellationToken);
            Assert.Empty(pending);
            Assert.True(await context.Database.CanConnectAsync(TestContext.Current.CancellationToken));
        }
    }

    private static async Task ExecuteAdminCommandAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    private static string ConnectionStringFor(string adminConnectionString, string databaseName)
    {
        var builder = new NpgsqlConnectionStringBuilder(adminConnectionString) { Database = databaseName };
        return builder.ConnectionString;
    }

    private static ServiceProvider RuntimePublisherProvider(string connectionString)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<BpmnDbContext>(options => options.UseVertexNpgsql(connectionString));
        return services.BuildServiceProvider();
    }

    private sealed class ConcurrentRecordingTransport : IRuntimeOutboxTransport
    {
        public ConcurrentDictionary<Guid, int> Deliveries { get; } = new();

        public ValueTask PublishAsync(RuntimeOutboxMessage message, CancellationToken cancellationToken = default)
        {
            Deliveries.AddOrUpdate(message.Id, 1, (_, count) => count + 1);
            return ValueTask.CompletedTask;
        }

        public ValueTask<OutboxTransportHealth> CheckHealthAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(new OutboxTransportHealth(true, "test transport ready"));
    }
}
