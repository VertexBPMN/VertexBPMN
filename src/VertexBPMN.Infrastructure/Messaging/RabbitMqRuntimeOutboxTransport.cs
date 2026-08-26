using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using VertexBPMN.Domain.Entities;

namespace VertexBPMN.Infrastructure.Messaging;

public sealed class RabbitMqRuntimeOutboxTransport(RuntimeOutboxOptions options) : IRuntimeOutboxTransport
{
    private ConnectionFactory CreateFactory() => new()
    {
        Uri = new Uri(options.ConnectionString ?? throw new InvalidOperationException(
            "Runtime:Outbox:ConnectionString is required for RabbitMQ."))
    };

    public async ValueTask PublishAsync(RuntimeOutboxMessage message, CancellationToken cancellationToken = default)
    {
        await using var connection = await CreateFactory().CreateConnectionAsync(cancellationToken);
        await using var channel = await connection.CreateChannelAsync(
            new CreateChannelOptions(
                publisherConfirmationsEnabled: true,
                publisherConfirmationTrackingEnabled: true),
            cancellationToken);
        await channel.ExchangeDeclareAsync(
            options.Destination,
            ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            cancellationToken: cancellationToken);

        var envelope = JsonSerializer.Serialize(new
        {
            id = message.Id,
            eventType = message.EventType,
            processInstanceId = message.ProcessInstanceId,
            tenantId = message.TenantId,
            occurredAt = message.OccurredAt,
            payload = JsonDocument.Parse(message.Payload).RootElement
        });
        var properties = new BasicProperties
        {
            ContentType = "application/json",
            DeliveryMode = DeliveryModes.Persistent,
            MessageId = message.Id.ToString("N"),
            Type = message.EventType,
            Timestamp = new AmqpTimestamp(new DateTimeOffset(message.OccurredAt).ToUnixTimeSeconds())
        };

        await channel.BasicPublishAsync(
            options.Destination,
            message.EventType,
            mandatory: true,
            basicProperties: properties,
            body: Encoding.UTF8.GetBytes(envelope),
            cancellationToken: cancellationToken);
    }

    public async ValueTask<OutboxTransportHealth> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = await CreateFactory().CreateConnectionAsync(cancellationToken);
            await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);
            return new OutboxTransportHealth(connection.IsOpen && channel.IsOpen, "RabbitMQ connection established.");
        }
        catch (Exception ex)
        {
            return new OutboxTransportHealth(false, $"RabbitMQ unavailable: {ex.Message}");
        }
    }
}
