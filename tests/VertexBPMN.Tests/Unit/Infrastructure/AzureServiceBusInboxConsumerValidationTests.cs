using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using VertexBPMN.Infrastructure.Messaging;

namespace VertexBPMN.Tests.Unit.Infrastructure;

/// <summary>
/// P8.1: configuration-error validation of the Azure Service Bus inbox consumer
/// (<see cref="AzureServiceBusRuntimeInboxConsumerService"/>). The constructor
/// validates options before any client is created, so these run without a broker.
/// </summary>
public sealed class AzureServiceBusInboxConsumerValidationTests
{
    private static RuntimeOutboxOptions BaseOutbox() => new()
    {
        Provider = "AzureServiceBus",
        EntityName = "vertexbpmn-runtime",
        EntityType = RuntimeOutboxEntityType.Topic,
        AuthenticationMode = RuntimeOutboxAuthenticationMode.ManagedIdentity,
        FullyQualifiedNamespace = "test.servicebus.windows.net"
    };

    private static RuntimeInboxOptions BaseInbox() => new() { Subscription = "all" };

    private static IServiceScopeFactory ScopeFactory() =>
        new ServiceCollection().BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();

    private static void Build(RuntimeOutboxOptions outbox, RuntimeInboxOptions inbox)
    {
        _ = new AzureServiceBusRuntimeInboxConsumerService(
            ScopeFactory(), outbox, inbox, NullLogger<AzureServiceBusRuntimeInboxConsumerService>.Instance);
    }

    [Fact]
    public void Topic_Without_Subscription_IsRejected()
    {
        var outbox = BaseOutbox();
        outbox.EntityType = RuntimeOutboxEntityType.Topic; // default topic
        var inbox = BaseInbox();
        inbox.Subscription = string.Empty;

        var error = Assert.Throws<InvalidOperationException>(() => Build(outbox, inbox));
        Assert.Contains("Inbox:Subscription", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Queue_DoesNotRequire_Subscription()
    {
        var outbox = BaseOutbox();
        outbox.EntityType = RuntimeOutboxEntityType.Queue;
        var inbox = BaseInbox();
        inbox.Subscription = string.Empty;

        // Must not throw: a queue has no subscription.
        Build(outbox, inbox);
    }

    [Fact]
    public void ManagedIdentity_Without_Namespace_IsRejected()
    {
        var outbox = BaseOutbox();
        outbox.FullyQualifiedNamespace = null;
        outbox.AuthenticationMode = RuntimeOutboxAuthenticationMode.ManagedIdentity;

        var error = Assert.Throws<InvalidOperationException>(() => Build(outbox, BaseInbox()));
        Assert.Contains("FullyQualifiedNamespace", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ConnectionString_Mode_DoesNot_Require_Namespace()
    {
        var outbox = BaseOutbox();
        outbox.AuthenticationMode = RuntimeOutboxAuthenticationMode.ConnectionString;
        outbox.FullyQualifiedNamespace = null;
        outbox.ConnectionString = "Endpoint=sb://placeholder.servicebus.windows.net/;SharedAccessKeyName=x;SharedAccessKey=y;";

        // Validation is deliberately name-only and does not connect to the endpoint.
        Build(outbox, BaseInbox());
    }

    [Fact]
    public void Missing_EntityName_IsRejected()
    {
        var outbox = BaseOutbox();
        outbox.EntityName = null;

        var error = Assert.Throws<InvalidOperationException>(() => Build(outbox, BaseInbox()));
        Assert.Contains("EntityName", error.Message, StringComparison.Ordinal);
    }
}
