using System.Text;
using System.Text.Json;
using VertexBPMN.Infrastructure.Messaging;

namespace VertexBPMN.Tests.Unit.Infrastructure;

/// <summary>
/// P8.1: wire-format contract of the runtime-inbox envelope parser
/// (<see cref="InboxEnvelope.Parse"/>), the exact provider-neutral shape produced by
/// every transport (RabbitMQ, Kafka, Azure Service Bus). Verifies id formats, optional
/// field normalization and malformed-payload rejection — without a live broker.
/// </summary>
public sealed class InboxEnvelopeParseTests
{
    private const string NFormat = "0f8fad5bd9cb469fa16570867728950e";
    private const string DFormat = "0f8fad5b-d9cb-469f-a165-70867728950e";

    private static byte[] Wire(params (string name, string json)[] props)
    {
        var body = "{" + string.Join(",", props.Select(p => $"\"{p.name}\":{p.json}")) + "}";
        return Encoding.UTF8.GetBytes(body);
    }

    private static byte[] FullWire(string idJson) => Wire(
        ("id", idJson),
        ("eventType", "\"ProcessInstanceStarted\""),
        ("processInstanceId", "\"6ba7b810-9dad-11d1-80b4-00c04fd430c8\""),
        ("tenantId", "\"tenant-42\""),
        ("occurredAt", "\"2026-09-21T12:00:00Z\""),
        ("payload", "{\"key\":\"value\"}"));

    [Fact]
    public void Parse_AcceptsNFormatId()
    {
        var envelope = InboxEnvelope.Parse(FullWire($"\"{NFormat}\""));

        Assert.NotNull(envelope.Id);
        Assert.Equal(Guid.Parse(NFormat), envelope.Id);
    }

    [Fact]
    public void Parse_AcceptsDCanonicalFormatId()
    {
        var envelope = InboxEnvelope.Parse(FullWire($"\"{DFormat}\""));

        Assert.NotNull(envelope.Id);
        Assert.Equal(Guid.Parse(DFormat), envelope.Id);
    }

    [Fact]
    public void Parse_ReadsEventTypeTenantAndProcessInstance()
    {
        var envelope = InboxEnvelope.Parse(FullWire($"\"{DFormat}\""));

        Assert.Equal("ProcessInstanceStarted", envelope.EventType);
        Assert.Equal("tenant-42", envelope.TenantId);
        Assert.Equal(Guid.Parse("6ba7b810-9dad-11d1-80b4-00c04fd430c8"), envelope.ProcessInstanceId);
        Assert.Equal(DateTimeOffset.Parse("2026-09-21T12:00:00Z"), envelope.OccurredAt);
    }

    [Fact]
    public void Parse_ClonesPayload_SoItOutlivesSourceDocument()
    {
        var envelope = InboxEnvelope.Parse(FullWire($"\"{DFormat}\""));

        // Access after Parse's internal JsonDocument has been disposed.
        Assert.Equal("value", envelope.Payload!.Value.GetProperty("key").GetString());
    }

    [Theory]
    [InlineData("{\"eventType\":\"E\"}")]                 // no id at all
    [InlineData("{\"id\":\"\",\"eventType\":\"E\"}")]     // empty id string
    [InlineData("{\"id\":123,\"eventType\":\"E\"}")]      // id not a string
    [InlineData("{\"id\":\"not-a-guid\",\"eventType\":\"E\"}")] // invalid guid
    public void Parse_NormalizesAbsentOrInvalidIdToNull(string json)
    {
        var envelope = InboxEnvelope.Parse(Encoding.UTF8.GetBytes(json));

        Assert.Null(envelope.Id);
    }

    [Fact]
    public void Parse_NullTenantNormalizesToNull()
    {
        var json = "{\"eventType\":\"E\",\"tenantId\":null,\"payload\":{}}";
        var envelope = InboxEnvelope.Parse(Encoding.UTF8.GetBytes(json));

        Assert.Null(envelope.TenantId);
        Assert.Equal("E", envelope.EventType);
    }

    [Fact]
    public void Parse_MissingPayloadIsNull()
    {
        var json = "{\"eventType\":\"E\"}";
        var envelope = InboxEnvelope.Parse(Encoding.UTF8.GetBytes(json));

        Assert.Null(envelope.Payload);
    }

    [Fact]
    public void Parse_MalformedJsonThrows_SoConsumerDeadLetters()
    {
        // The consumer catches a parse exception and moves the message to the DLQ
        // ("unparseable_envelope") rather than acknowledging it.
        Assert.ThrowsAny<JsonException>(() => InboxEnvelope.Parse(Encoding.UTF8.GetBytes("{not-json")));
    }

    [Fact]
    public void Parse_EmptyBodyThrows()
    {
        Assert.ThrowsAny<JsonException>(() => InboxEnvelope.Parse(Array.Empty<byte>()));
    }
}
