using System.Text;
using System.Text.Json;
using Confluent.Kafka;
using VertexBPMN.Domain.Entities;

namespace VertexBPMN.Infrastructure.Messaging;

public sealed class KafkaRuntimeOutboxTransport : IRuntimeOutboxTransport, IDisposable
{
    private readonly RuntimeOutboxOptions _options;
    private readonly IProducer<string, string> _producer;
    private readonly IAdminClient _adminClient;

    public KafkaRuntimeOutboxTransport(RuntimeOutboxOptions options)
    {
        _options = options;
        var bootstrapServers = options.ConnectionString ?? throw new InvalidOperationException(
            "Runtime:Outbox:ConnectionString is required for Kafka.");
        _producer = new ProducerBuilder<string, string>(new ProducerConfig
        {
            BootstrapServers = bootstrapServers,
            EnableIdempotence = true,
            Acks = Acks.All,
            MessageSendMaxRetries = 5
        }).Build();
        _adminClient = new AdminClientBuilder(new AdminClientConfig
        {
            BootstrapServers = bootstrapServers
        }).Build();
    }

    public async ValueTask PublishAsync(RuntimeOutboxMessage message, CancellationToken cancellationToken = default)
    {
        var envelope = JsonSerializer.Serialize(new
        {
            id = message.Id,
            eventType = message.EventType,
            processInstanceId = message.ProcessInstanceId,
            tenantId = message.TenantId,
            occurredAt = message.OccurredAt,
            payload = JsonDocument.Parse(message.Payload).RootElement
        });
        var kafkaMessage = new Message<string, string>
        {
            Key = message.ProcessInstanceId == Guid.Empty
                ? message.Id.ToString("N")
                : message.ProcessInstanceId.ToString("N"),
            Value = envelope,
            Headers = new Headers
            {
                { "vertexbpmn-message-id", Encoding.UTF8.GetBytes(message.Id.ToString("N")) },
                { "vertexbpmn-event-type", Encoding.UTF8.GetBytes(message.EventType) }
            }
        };
        await _producer.ProduceAsync(_options.Destination, kafkaMessage, cancellationToken);
    }

    public ValueTask<OutboxTransportHealth> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var metadata = _adminClient.GetMetadata(TimeSpan.FromSeconds(5));
            return ValueTask.FromResult(metadata.Brokers.Count > 0
                ? new OutboxTransportHealth(true, $"Kafka brokers available: {metadata.Brokers.Count}.")
                : new OutboxTransportHealth(false, "Kafka returned no available brokers."));
        }
        catch (Exception ex)
        {
            return ValueTask.FromResult(new OutboxTransportHealth(false, $"Kafka unavailable: {ex.Message}"));
        }
    }

    public void Dispose()
    {
        _producer.Flush(TimeSpan.FromSeconds(5));
        _producer.Dispose();
        _adminClient.Dispose();
    }
}
