using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using VertexBPMN.Infrastructure;
using VertexBPMN.Infrastructure.Messaging;

namespace VertexBPMN.Tests.Unit.Infrastructure;

/// <summary>
/// P8.1: configuration-validation matrix for the runtime outbox/inbox wiring
/// (<see cref="InfrastructureModule.AddBpmnPersistenceServices"/> →
/// <c>ConfigureRuntimeOutbox</c>). These run broker-free: they only inspect
/// whether registration throws or which registrar was picked, mirroring the
/// existing AzureServiceBusInboxConsumerValidationTests.
///
/// Covers the "Kafka-Outbox plus gültige Inbox-Konfiguration gesondert prüfen"
/// item from the P8 plan: a Kafka outbox publisher is supported, but Kafka has
/// no inbox consumer — enabling inbox with Provider=Kafka must be rejected
/// loudly, never a silent fallback to an unrelated consumer.
/// </summary>
public sealed class RuntimeOutboxProviderConfigurationTests
{
    private static readonly string TempDir =
        Path.Combine(Path.GetTempPath(), "vertexbpmn-test-keys");

    private static IConfiguration Config(IEnumerable<KeyValuePair<string, string?>> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    /// <summary>Base Stage config that satisfies the earlier InfrastructureModule
    /// guards (DependencyRegistry + DataProtection KeyRing) so the outbox/inbox
    /// validation under test is actually reached.</summary>
    private static Dictionary<string, string?> StageBase() => new()
    {
        ["OperationalMode"] = "Stage",
        ["ConnectionStrings:DependencyRegistry"] = $"Data Source={Path.Combine(Path.GetTempPath(), "vb-deps-test.db")}",
        ["DataProtection:KeyRingPath"] = TempDir,
        ["DataProtection:Provider"] = "LocalFilesystem"
    };

    [Fact]
    public void Kafka_Outbox_Without_Inbox_Is_Supported()
    {
        var configuration = Config(new Dictionary<string, string?>
        {
            ["OperationalMode"] = "Development",
            ["Runtime:Outbox:Enabled"] = "true",
            ["Runtime:Outbox:Provider"] = "Kafka",
            ["Runtime:Outbox:ConnectionString"] = "bootstrap.servers=localhost:9092",
            ["Runtime:Inbox:Enabled"] = "false"
        });

        var services = new ServiceCollection();
        services.AddBpmnPersistenceServices(configuration);

        // Publisher hosted service registered; inbox consumer must NOT be registered.
        Assert.Contains(services, d => d.ImplementationType == typeof(RuntimeOutboxPublisherService));
        Assert.DoesNotContain(services, d => d.ImplementationType == typeof(RuntimeInboxConsumerService));
        Assert.DoesNotContain(services, d => d.ImplementationType == typeof(AzureServiceBusRuntimeInboxConsumerService));
    }

    [Fact]
    public void Kafka_Plus_Inbox_Is_Rejected_Without_Silent_Fallback()
    {
        var values = StageBase();
        values["Runtime:Outbox:Enabled"] = "true";
        values["Runtime:Outbox:Provider"] = "Kafka";
        values["Runtime:Outbox:ConnectionString"] = "bootstrap.servers=localhost:9092";
        // Runtime:Inbox:Enabled unset => defaults to outbox.Enabled (true)

        var error = Assert.Throws<InvalidOperationException>(() =>
            new ServiceCollection().AddBpmnPersistenceServices(Config(values)));

        Assert.Contains("not supported for Provider=Kafka", error.Message, StringComparison.Ordinal);
        Assert.Contains("RabbitMQ or AzureServiceBus", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Stage_Rejects_Disabled_Outbox()
    {
        var values = StageBase();
        values["Runtime:Outbox:Enabled"] = "false";

        var error = Assert.Throws<InvalidOperationException>(() =>
            new ServiceCollection().AddBpmnPersistenceServices(Config(values)));

        Assert.Contains("Enabled must be true", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Stage_Rejects_Unknown_Provider()
    {
        var values = StageBase();
        values["Runtime:Outbox:Enabled"] = "true";
        values["Runtime:Outbox:Provider"] = "InMemory";
        values["Runtime:Outbox:ConnectionString"] = "amqp://localhost:5672";

        var error = Assert.Throws<InvalidOperationException>(() =>
            new ServiceCollection().AddBpmnPersistenceServices(Config(values)));

        Assert.Contains("Provider must be Kafka, RabbitMq or AzureServiceBus", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Enabled_Outbox_Without_ConnectionString_Is_Rejected()
    {
        var values = StageBase();
        values["Runtime:Outbox:Enabled"] = "true";
        values["Runtime:Outbox:Provider"] = "RabbitMq";

        var error = Assert.Throws<InvalidOperationException>(() =>
            new ServiceCollection().AddBpmnPersistenceServices(Config(values)));

        Assert.Contains("ConnectionString is required", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RabbitMq_Inbox_Registers_Inbox_Consumer()
    {
        var values = StageBase();
        values["Runtime:Outbox:Enabled"] = "true";
        values["Runtime:Outbox:Provider"] = "RabbitMq";
        values["Runtime:Outbox:ConnectionString"] = "amqp://localhost:5672";

        var services = new ServiceCollection();
        services.AddBpmnPersistenceServices(Config(values));

        Assert.Contains(services, d => d.ImplementationType == typeof(RuntimeOutboxPublisherService));
        Assert.Contains(services, d => d.ImplementationType == typeof(RuntimeInboxConsumerService));
        Assert.DoesNotContain(services, d => d.ImplementationType == typeof(AzureServiceBusRuntimeInboxConsumerService));
    }

    [Fact]
    public void AzureServiceBus_Inbox_Registers_Asb_Consumer_Not_Rabbit()
    {
        var values = StageBase();
        values["Runtime:Outbox:Enabled"] = "true";
        values["Runtime:Outbox:Provider"] = "AzureServiceBus";
        values["Runtime:Outbox:FullyQualifiedNamespace"] = "test.servicebus.windows.net";
        values["Runtime:Outbox:EntityName"] = "vertexbpmn-runtime";
        values["Runtime:Inbox:Subscription"] = "all";

        var services = new ServiceCollection();
        services.AddBpmnPersistenceServices(Config(values));

        Assert.DoesNotContain(services, d => d.ImplementationType == typeof(RuntimeInboxConsumerService));
        Assert.Contains(services, d => d.ImplementationType == typeof(AzureServiceBusRuntimeInboxConsumerService));
    }
}
