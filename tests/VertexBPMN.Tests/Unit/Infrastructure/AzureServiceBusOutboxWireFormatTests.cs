using System.Text.Json;
using Azure.Messaging.ServiceBus;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Infrastructure.Messaging;

namespace VertexBPMN.Tests.Unit.Infrastructure;

/// <summary>
/// P8.1: wire-format contract of the Azure Service Bus runtime-outbox transport.
/// Verifies the stable MessageId, the JSON envelope shape (provider-neutral, identical to
/// RabbitMQ/Kafka), correlation id and application properties — without a live broker.
/// </summary>
public sealed class AzureServiceBusOutboxWireFormatTests
{
    private static RuntimeOutboxMessage SampleMessage()
    {
        var id = Guid.Parse("0f8fad5b-d9cb-469f-a165-70867728950e");
        return new RuntimeOutboxMessage
        {
            Id = id,
            ProcessInstanceId = Guid.Parse("6ba7b810-9dad-11d1-80b4-00c04fd430c8"),
            EventType = "ProcessInstanceStarted",
            TenantId = "tenant-42",
            OccurredAt = new DateTime(2026, 9, 21, 12, 0, 0, DateTimeKind.Utc),
            Payload = "{\"key\":\"value\",\"n\":1}"
        };
    }

    [Fact]
    public void StableMessageId_IsTheOutboxIdInNFormat_SoRetriesCanBeDeDuplicated()
    {
        var message = SampleMessage();
        var wireMessage = AzureServiceBusRuntimeOutboxTransport.BuildServiceBusMessage(message);

        // The outbox id drives de-duplication across retries; it must survive a re-broadcast unchanged.
        Assert.Equal(message.Id.ToString("N"), wireMessage.MessageId);
        Assert.DoesNotContain("-", wireMessage.MessageId!);
    }

    [Fact]
    public void Envelope_PreservesProviderNeutralEventShape()
    {
        var wireMessage = AzureServiceBusRuntimeOutboxTransport.BuildServiceBusMessage(SampleMessage());

        using var doc = JsonDocument.Parse(wireMessage.Body.ToArray());
        var root = doc.RootElement;

        Assert.Equal("ProcessInstanceStarted", root.GetProperty("eventType").GetString());
        Assert.Equal("tenant-42", root.GetProperty("tenantId").GetString());
        // JsonSerializer emits Guid in its canonical (hyphenated, "D") form inside the
        // provider-neutral envelope; round-trip it to prove identity preservation.
        Assert.Equal(
            SampleMessage().ProcessInstanceId,
            root.GetProperty("processInstanceId").GetGuid());
        Assert.Equal(
            "2026-09-21T12:00:00.0000000Z",
            root.GetProperty("occurredAt").GetDateTimeOffset().UtcDateTime.ToString("o"));
        Assert.Equal("value", root.GetProperty("payload").GetProperty("key").GetString());

        Assert.Equal("application/json", wireMessage.ContentType);
        Assert.Equal("ProcessInstanceStarted", wireMessage.Subject);
    }

    [Fact]
    public void CorrelationId_TracksTheProcessInstanceInNFormat()
    {
        var wireMessage = AzureServiceBusRuntimeOutboxTransport.BuildServiceBusMessage(SampleMessage());

        Assert.Equal(
            SampleMessage().ProcessInstanceId.ToString("N"),
            wireMessage.CorrelationId);
    }

    [Fact]
    public void ApplicationProperties_CarryEventTenantAndProcessMetadata()
    {
        var wireMessage = AzureServiceBusRuntimeOutboxTransport.BuildServiceBusMessage(SampleMessage());

        Assert.Equal("ProcessInstanceStarted", wireMessage.ApplicationProperties["eventType"]);
        Assert.Equal("tenant-42", wireMessage.ApplicationProperties["tenantId"]);
        Assert.Equal(
            SampleMessage().ProcessInstanceId.ToString("N"),
            wireMessage.ApplicationProperties["processInstanceId"]);
    }

    [Fact]
    public void EmptyProcessInstanceId_ProducesNullCorrelationId()
    {
        var message = SampleMessage();
        message.ProcessInstanceId = Guid.Empty;

        var wireMessage = AzureServiceBusRuntimeOutboxTransport.BuildServiceBusMessage(message);

        Assert.Null(wireMessage.CorrelationId);
        Assert.Equal(Guid.Empty.ToString("N"), wireMessage.ApplicationProperties["processInstanceId"]);
    }
}
