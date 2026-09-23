using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using VertexBPMN.Infrastructure.Persistence;

namespace VertexBPMN.Infrastructure.Messaging;

/// <summary>
/// Produktioneller RabbitMQ-Inbox-Konsument: bindet eine durable Queue an die
/// Runtime-Outbox-Destination-Exchange und verarbeitet eingehende Envelopes idempotent
/// über den geteilten <see cref="RuntimeInboxProcessor"/>.
///
/// Ack-/Nack-Semantik (at-least-once + Last-Recovery via Claim-Timeout):
///  - Completed / CompletedDuplicate  -> Ack (abgeschlossene Verarbeitung / bereits abgeschlossen)
///  - Busy                            -> Nack(requeue)  (Claim noch nicht abgelaufen; spaeter uebernehmbar)
///  - RetryableFailure                -> Nack(requeue)  (transienter Fehler)
///  - Rejected                        -> Nack(no-requeue) -> Dead-Letter-Queue (DLX)
/// </summary>
public sealed class RuntimeInboxConsumerService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly RuntimeOutboxOptions _options;
    private readonly ILogger<RuntimeInboxConsumerService> _logger;
    private readonly RuntimeInboxProcessor _processor;
    private readonly string _queueName;
    private readonly string _dlxName;
    private readonly string _dlqName;

    public RuntimeInboxConsumerService(
        IServiceScopeFactory scopeFactory,
        RuntimeOutboxOptions options,
        RuntimeInboxOptions inboxOptions,
        ILogger<RuntimeInboxConsumerService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
        _processor = new RuntimeInboxProcessor(scopeFactory, inboxOptions, NullLogger<RuntimeInboxProcessor>.Instance);
        _queueName = $"inbox:{SanitizeQueueSuffix(options.Destination)}";
        _dlxName = $"{_queueName}.dlx";
        _dlqName = $"{_queueName}.dlq";
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunConsumeLoopAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Runtime inbox consumer connection failed; retrying in {Delay}s.",
                    _options.PollIntervalMilliseconds / 1000.0);
                await Task.Delay(TimeSpan.FromMilliseconds(_options.PollIntervalMilliseconds), stoppingToken);
            }
        }
    }

    private async Task RunConsumeLoopAsync(CancellationToken stoppingToken)
    {
        var factory = new ConnectionFactory
        {
            Uri = new Uri(_options.ConnectionString ?? throw new InvalidOperationException(
                "Runtime:Outbox:ConnectionString is required for the RabbitMQ inbox consumer."))
        };
        await using var connection = await factory.CreateConnectionAsync(stoppingToken);
        await using var channel = await connection.CreateChannelAsync(
            new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true),
            stoppingToken);

        await channel.ExchangeDeclareAsync(
            _options.Destination,
            ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            cancellationToken: stoppingToken);

        // Dead-letter exchange + queue for permanently rejected (poison) messages.
        await channel.ExchangeDeclareAsync(_dlxName, ExchangeType.Direct, durable: true, autoDelete: false,
            cancellationToken: stoppingToken);
        await channel.QueueDeclareAsync(_dlqName, durable: true, exclusive: false, autoDelete: false,
            cancellationToken: stoppingToken);
        await channel.QueueBindAsync(_dlqName, _dlxName, _dlqName, cancellationToken: stoppingToken);

        // Durable, shared queue so work survives a restart and can be shared across replicas
        // (competing consumers). Permanent rejects are routed to the DLQ by the DLX argument.
        var queueArguments = new Dictionary<string, object?>
        {
            ["x-dead-letter-exchange"] = _dlxName,
            ["x-dead-letter-routing-key"] = _dlqName
        };
        await channel.QueueDeclareAsync(_queueName, durable: true, exclusive: false, autoDelete: false,
            arguments: queueArguments, cancellationToken: stoppingToken);
        await channel.QueueBindAsync(_queueName, _options.Destination, "#", cancellationToken: stoppingToken);

        _logger.LogInformation("Runtime inbox consumer listening on queue '{Queue}' for exchange '{Destination}' (DLQ '{Dlq}').",
            _queueName, _options.Destination, _dlqName);

        var consumer = new AsyncEventingBasicConsumer(channel);
        var deliverChannel = channel;
        consumer.ReceivedAsync += async (_, deliverEventArgs) =>
        {
            await HandleDeliveryAsync(deliverChannel, deliverEventArgs, stoppingToken);
        };
        await channel.BasicConsumeAsync(_queueName, autoAck: false, consumer: consumer, cancellationToken: stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
            await Task.Delay(TimeSpan.FromMilliseconds(_options.PollIntervalMilliseconds), stoppingToken);
    }

    private async Task HandleDeliveryAsync(
        IChannel channel,
        BasicDeliverEventArgs deliverEventArgs,
        CancellationToken stoppingToken)
    {
        InboxEnvelope envelope;
        string messageId;
        try
        {
            envelope = InboxEnvelope.Parse(deliverEventArgs.Body.ToArray());
            messageId = deliverEventArgs.BasicProperties.MessageId;
            if (string.IsNullOrWhiteSpace(messageId))
                messageId = envelope.Id?.ToString("N") ?? Guid.NewGuid().ToString("N");
        }
        catch (Exception ex)
        {
            // Unparseable envelope: permanent, cannot ever succeed -> dead-letter.
            _logger.LogError(ex, "Inbox envelope could not be parsed; dead-lettering (DeliveryTag {Tag}).",
                deliverEventArgs.DeliveryTag);
            await channel.BasicNackAsync(deliverEventArgs.DeliveryTag, multiple: false, requeue: false,
                cancellationToken: stoppingToken);
            return;
        }

        try
        {
            var result = await _processor.ProcessAsync(envelope, messageId, stoppingToken);
            switch (result.Outcome)
            {
                case RuntimeInboxOutcome.Completed:
                    await channel.BasicAckAsync(deliverEventArgs.DeliveryTag, multiple: false, cancellationToken: stoppingToken);
                    _logger.LogInformation("Inbox message {MessageId} ({Event}) processed exactly once.",
                        messageId, envelope.EventType);
                    break;
                case RuntimeInboxOutcome.CompletedDuplicate:
                    await channel.BasicAckAsync(deliverEventArgs.DeliveryTag, multiple: false, cancellationToken: stoppingToken);
                    _logger.LogDebug("Inbox message {MessageId} ({Event}) already completed; idempotent no-op.",
                        messageId, envelope.EventType);
                    break;
                case RuntimeInboxOutcome.Busy:
                    // Claim not yet expired and held by another worker -> leave in queue so a later
                    // redelivery can reclaim it once the claim times out.
                    await channel.BasicNackAsync(deliverEventArgs.DeliveryTag, multiple: false, requeue: true,
                        cancellationToken: stoppingToken);
                    _logger.LogDebug("Inbox message {MessageId} ({Event}) busy (claim held); requeueing.", messageId, envelope.EventType);
                    break;
                case RuntimeInboxOutcome.RetryableFailure:
                    await channel.BasicNackAsync(deliverEventArgs.DeliveryTag, multiple: false, requeue: true,
                        cancellationToken: stoppingToken);
                    _logger.LogWarning("Inbox message {MessageId} ({Event}) failed transiently; requeueing (at-least-once). Reason: {Reason}",
                        messageId, envelope.EventType, result.Reason);
                    break;
                case RuntimeInboxOutcome.Rejected:
                    await channel.BasicNackAsync(deliverEventArgs.DeliveryTag, multiple: false, requeue: false,
                        cancellationToken: stoppingToken);
                    _logger.LogError("Inbox message {MessageId} ({Event}) permanently rejected; dead-lettered. Reason: {Reason}",
                        messageId, envelope.EventType, result.Reason);
                    break;
                default:
                    // Unknown outcome: treat as transient so nothing is silently acked.
                    await channel.BasicNackAsync(deliverEventArgs.DeliveryTag, multiple: false, requeue: true,
                        cancellationToken: stoppingToken);
                    break;
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Inbox message {MessageId} ({Event}) could not be settled after processing; requeueing.",
                messageId, envelope.EventType);
            await channel.BasicNackAsync(deliverEventArgs.DeliveryTag, multiple: false, requeue: true,
                cancellationToken: stoppingToken);
        }
    }

    private static string SanitizeQueueSuffix(string destination)
    {
        var invalid = destination.Where(ch => !char.IsLetterOrDigit(ch) && ch != '-' && ch != '_').ToArray();
        return invalid.Length == 0 ? destination
            : new string(destination.Select(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' ? ch : '-').ToArray());
    }
}
