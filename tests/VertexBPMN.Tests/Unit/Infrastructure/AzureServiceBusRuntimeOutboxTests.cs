using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using VertexBPMN.Infrastructure;
using VertexBPMN.Infrastructure.Messaging;

namespace VertexBPMN.Tests.Unit.Infrastructure;

/// <summary>
/// P1: Azure Service Bus as an additional runtime-outbox provider.
/// These tests validate provider wiring and configuration errors without a real namespace.
/// </summary>
public sealed class AzureServiceBusRuntimeOutboxTests
{
    [Fact]
    public async Task Production_accepts_AzureServiceBus_managed_identity_and_registers_transport()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["OperationalMode"] = "Production",
            ["ConnectionStrings:DependencyRegistry"] = "Data Source=:memory:",
            ["DataProtection:KeyRingPath"] = Path.Combine(Path.GetTempPath(), "vertexbpmn-asb-test-keys"),
            ["Runtime:Outbox:Enabled"] = "true",
            ["Runtime:Outbox:Provider"] = "AzureServiceBus",
            ["Runtime:Outbox:EntityName"] = "vertexbpmn-runtime",
            ["Runtime:Outbox:EntityType"] = "Topic",
            ["Runtime:Outbox:AuthenticationMode"] = "ManagedIdentity",
            ["Runtime:Outbox:FullyQualifiedNamespace"] = "test.servicebus.windows.net"
        });
        var services = new ServiceCollection();

        services.AddBpmnPersistenceServices(configuration);
        await using var provider = services.BuildServiceProvider();

        // The transport is registered via a factory; resolve to verify the concrete type.
        Assert.IsType<AzureServiceBusRuntimeOutboxTransport>(
            provider.GetRequiredService<IRuntimeOutboxTransport>());

        // A Service Bus deployment must not start a RabbitMQ inbox consumer (deferred to P2).
        Assert.DoesNotContain(
            services,
            d => d.ServiceType == typeof(IHostedService)
                 && d.ImplementationType == typeof(RuntimeInboxConsumerService));
    }

    [Fact]
    public void Production_AzureServiceBus_managed_identity_does_not_require_connection_string()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["OperationalMode"] = "Production",
            ["ConnectionStrings:DependencyRegistry"] = "Data Source=:memory:",
            ["DataProtection:KeyRingPath"] = Path.Combine(Path.GetTempPath(), "vertexbpmn-asb-test-keys"),
            ["Runtime:Outbox:Enabled"] = "true",
            ["Runtime:Outbox:Provider"] = "AzureServiceBus",
            ["Runtime:Outbox:EntityName"] = "vertexbpmn-runtime",
            ["Runtime:Outbox:FullyQualifiedNamespace"] = "test.servicebus.windows.net"
        });

        // No ConnectionString is provided; this must not throw because Managed Identity is selected.
        new ServiceCollection().AddBpmnPersistenceServices(configuration);
    }

    [Fact]
    public void Production_AzureServiceBus_managed_identity_requires_namespace()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["OperationalMode"] = "Production",
            ["ConnectionStrings:DependencyRegistry"] = "Data Source=:memory:",
            ["DataProtection:KeyRingPath"] = Path.Combine(Path.GetTempPath(), "vertexbpmn-asb-test-keys"),
            ["Runtime:Outbox:Enabled"] = "true",
            ["Runtime:Outbox:Provider"] = "AzureServiceBus",
            ["Runtime:Outbox:EntityName"] = "vertexbpmn-runtime"
        });
        var services = new ServiceCollection();
        services.AddBpmnPersistenceServices(configuration);

        using var provider = services.BuildServiceProvider();
        var exception = Assert.Throws<InvalidOperationException>(
            () => provider.GetRequiredService<IRuntimeOutboxTransport>());

        Assert.Contains("FullyQualifiedNamespace", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Production_rejects_unknown_outbox_provider()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["OperationalMode"] = "Production",
            ["ConnectionStrings:DependencyRegistry"] = "Data Source=:memory:",
            ["DataProtection:KeyRingPath"] = Path.Combine(Path.GetTempPath(), "vertexbpmn-asb-test-keys"),
            ["Runtime:Outbox:Enabled"] = "true",
            ["Runtime:Outbox:Provider"] = "Sqs",
            ["Runtime:Outbox:ConnectionString"] = "not-empty"
        });

        var exception = Assert.Throws<InvalidOperationException>(
            () => new ServiceCollection().AddBpmnPersistenceServices(configuration));

        Assert.Contains("Kafka, RabbitMq or AzureServiceBus", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Production_AzureServiceBus_connection_string_mode_requires_connection_string()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["OperationalMode"] = "Production",
            ["ConnectionStrings:DependencyRegistry"] = "Data Source=:memory:",
            ["DataProtection:KeyRingPath"] = Path.Combine(Path.GetTempPath(), "vertexbpmn-asb-test-keys"),
            ["Runtime:Outbox:Enabled"] = "true",
            ["Runtime:Outbox:Provider"] = "AzureServiceBus",
            ["Runtime:Outbox:EntityName"] = "vertexbpmn-runtime",
            ["Runtime:Outbox:AuthenticationMode"] = "ConnectionString",
            ["Runtime:Outbox:FullyQualifiedNamespace"] = "test.servicebus.windows.net"
        });
        var services = new ServiceCollection();

        // With AuthenticationMode=ConnectionString but no ConnectionString, the DI guard
        // already fails at registration time.
        var exception = Assert.Throws<InvalidOperationException>(
            () => services.AddBpmnPersistenceServices(configuration));

        Assert.Contains("ConnectionString", exception.Message, StringComparison.Ordinal);
    }

    private static IConfiguration BuildConfiguration(IEnumerable<KeyValuePair<string, string?>> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    [Fact]
    public void Production_RabbitMq_enabled_registers_rabbitmq_inbox_consumer_not_asb()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["OperationalMode"] = "Production",
            ["ConnectionStrings:DependencyRegistry"] = "Data Source=:memory:",
            ["DataProtection:KeyRingPath"] = Path.Combine(Path.GetTempPath(), "vertexbpmn-inbox-test-keys"),
            ["Runtime:Outbox:Enabled"] = "true",
            ["Runtime:Outbox:Provider"] = "RabbitMq",
            ["Runtime:Outbox:ConnectionString"] = "amqp://guest:guest@localhost:5672/"
        });
        var services = new ServiceCollection();
        services.AddBpmnPersistenceServices(configuration);

        Assert.Contains(services, d => d.ServiceType == typeof(IHostedService)
                                       && d.ImplementationType == typeof(RuntimeInboxConsumerService));
        // A RabbitMQ deployment must not start an Azure Service Bus consumer.
        Assert.DoesNotContain(services, d => d.ServiceType == typeof(IHostedService)
                                             && d.ImplementationType == typeof(AzureServiceBusRuntimeInboxConsumerService));
    }

    [Fact]
    public void Production_AzureServiceBus_enabled_registers_asb_inbox_consumer_not_rabbitmq()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["OperationalMode"] = "Production",
            ["ConnectionStrings:DependencyRegistry"] = "Data Source=:memory:",
            ["DataProtection:KeyRingPath"] = Path.Combine(Path.GetTempPath(), "vertexbpmn-inbox-test-keys"),
            ["Runtime:Outbox:Enabled"] = "true",
            ["Runtime:Outbox:Provider"] = "AzureServiceBus",
            ["Runtime:Outbox:FullyQualifiedNamespace"] = "test.servicebus.windows.net",
            ["Runtime:Outbox:EntityName"] = "vertexbpmn-runtime",
            ["Runtime:Outbox:EntityType"] = "Topic",
            ["Runtime:Outbox:AuthenticationMode"] = "ManagedIdentity",
            ["Runtime:Inbox:Subscription"] = "all"
        });
        var services = new ServiceCollection();
        services.AddBpmnPersistenceServices(configuration);

        Assert.Contains(services, d => d.ServiceType == typeof(IHostedService)
                                       && d.ImplementationType == typeof(AzureServiceBusRuntimeInboxConsumerService));
        // A Service Bus deployment must not start a RabbitMQ consumer.
        Assert.DoesNotContain(services, d => d.ServiceType == typeof(IHostedService)
                                             && d.ImplementationType == typeof(RuntimeInboxConsumerService));
    }

    [Fact]
    public void Production_Kafka_enabled_inbox_throws_explicitly()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["OperationalMode"] = "Production",
            ["ConnectionStrings:DependencyRegistry"] = "Data Source=:memory:",
            ["DataProtection:KeyRingPath"] = Path.Combine(Path.GetTempPath(), "vertexbpmn-inbox-test-keys"),
            ["Runtime:Outbox:Enabled"] = "true",
            ["Runtime:Outbox:Provider"] = "Kafka",
            ["Runtime:Outbox:ConnectionString"] = "localhost:9092"
        });

        // Kafka outbox publishing remains, but inbox consumption is unsupported -> explicit error, no
        // silent RabbitMQ fallback.
        var exception = Assert.Throws<InvalidOperationException>(
            () => new ServiceCollection().AddBpmnPersistenceServices(configuration));
        Assert.Contains("Kafka", exception.Message, StringComparison.Ordinal);
        Assert.Contains("inbox", exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}
