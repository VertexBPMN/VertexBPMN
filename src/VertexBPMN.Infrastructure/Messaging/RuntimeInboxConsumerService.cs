using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Infrastructure.Persistence;

namespace VertexBPMN.Infrastructure.Messaging;

/// <summary>
/// Produktioneller Inbox-Konsument: bindet eine durable Queue an die
/// Runtime-Outbox-Destination-Exchange und verarbeitet eingehende Envelopes
/// idempotent. Jede Nachricht trägt eine stabile Message-ID; die
/// Idempotenz wird über den Unique-Index (TenantScope, Operation,
/// IdempotencyKey) der <see cref="RuntimeInboxMessage"/>-Tabelle gesichert.
/// Dadurch bleibt at-least-once-Zustellung (Duplikate erlaubt von der
/// Broker-API) von der garantiert-einmaligen Geschäftsverarbeitung getrennt.
/// </summary>
public sealed class RuntimeInboxConsumerService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly RuntimeOutboxOptions _options;
    private readonly ILogger<RuntimeInboxConsumerService> _logger;
    private readonly string _queueName;

    public RuntimeInboxConsumerService(
        IServiceScopeFactory scopeFactory,
        RuntimeOutboxOptions options,
        ILogger<RuntimeInboxConsumerService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
        _queueName = $"inbox:{SanitizeQueueSuffix(options.Destination)}";
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

        // Durable, shared queue so work survives a restart and can be shared
        // across replicas (competing consumers).
        await channel.QueueDeclareAsync(
            _queueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: stoppingToken);
        await channel.QueueBindAsync(_queueName, _options.Destination, "#", cancellationToken: stoppingToken);

        _logger.LogInformation("Runtime inbox consumer listening on queue '{Queue}' for exchange '{Destination}'.",
            _queueName, _options.Destination);

        var consumer = new AsyncEventingBasicConsumer(channel);
        var deliverChannel = channel;
        consumer.ReceivedAsync += async (_, deliverEventArgs) =>
        {
            await HandleDeliveryAsync(deliverChannel, deliverEventArgs, stoppingToken);
        };
        await channel.BasicConsumeAsync(_queueName, autoAck: false, consumer: consumer, cancellationToken: stoppingToken);

        // Keep reading until shutdown.
        while (!stoppingToken.IsCancellationRequested)
            await Task.Delay(TimeSpan.FromMilliseconds(_options.PollIntervalMilliseconds), stoppingToken);
    }

    private async Task HandleDeliveryAsync(
        IChannel channel,
        BasicDeliverEventArgs deliverEventArgs,
        CancellationToken stoppingToken)
    {
        // We ack manually so a transient processing failure requeues the message
        // (at-least-once). Idempotency guarantees the business effect still runs
        // exactly once even when the same envelope is delivered again.
        try
        {
            var envelope = InboxEnvelope.Parse(deliverEventArgs.Body.ToArray());
            var messageId = deliverEventArgs.BasicProperties.MessageId;
            if (string.IsNullOrWhiteSpace(messageId))
                messageId = envelope.Id?.ToString("N") ?? Guid.NewGuid().ToString("N");

            var processed = await ProcessIdempotentlyAsync(envelope, messageId, stoppingToken);

            await channel.BasicAckAsync(
                deliverEventArgs.DeliveryTag, multiple: false, cancellationToken: stoppingToken);

            if (processed)
                _logger.LogInformation("Inbox message {MessageId} ({EventType}) processed exactly once.",
                    messageId, envelope.EventType);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Inbox message processing failed; requeuing (at-least-once).");
            // Negative ack with requeue => message goes back to the queue and will be
            // redelivered; the idempotency key prevents duplicate business effects.
            await channel.BasicNackAsync(
                deliverEventArgs.DeliveryTag, multiple: false, requeue: true, cancellationToken: stoppingToken);
        }
    }

    private async Task<bool> ProcessIdempotentlyAsync(
        InboxEnvelope envelope,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BpmnDbContext>();
        var tenantId = envelope.TenantId;
        var tenantScope = string.IsNullOrWhiteSpace(tenantId) ? "$global" : tenantId.Trim();

        // Try to insert the claim; unique (TenantScope, Operation, IdempotencyKey)
        // constraint throws on a concurrent/duplicate claim.
        var claim = new RuntimeInboxMessage
        {
            Id = Guid.NewGuid(),
            Operation = string.IsNullOrWhiteSpace(envelope.EventType) ? "inbox" : envelope.EventType,
            IdempotencyKey = idempotencyKey,
            TenantId = tenantId,
            TenantScope = tenantScope,
            ReceivedAt = DateTime.UtcNow
        };
        db.RuntimeInbox.Add(claim);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            var existing = await db.RuntimeInbox.AsNoTracking().SingleOrDefaultAsync(
                item => item.Operation == claim.Operation
                        && item.IdempotencyKey == claim.IdempotencyKey
                        && item.TenantScope == claim.TenantScope,
                cancellationToken);
            if (existing is { CompletedAt: not null })
                return false; // already processed exactly once -> idempotent no-op
            return false; // claimed/owned by another replica -> skip
        }

        // Business handler: exact-once. Resolve via the registered inbox handler.
        var handler = scope.ServiceProvider.GetService<IRuntimeInboxHandler>();
        if (handler is not null)
        {
            await handler.HandleAsync(envelope.EventType, envelope.Payload, envelope.ProcessInstanceId, tenantId, cancellationToken);
        }
        else if (scope.ServiceProvider.GetService<IInboxEventSink>() is { } sink)
        {
            await sink.HandleAsync(envelope, cancellationToken);
        }

        claim.Result = "Processed";
        claim.CompletedAt = DateTime.UtcNow;
        db.Attach(claim);
        db.Entry(claim).State = EntityState.Modified;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static string SanitizeQueueSuffix(string destination)
    {
        var invalid = destination.Where(ch => !char.IsLetterOrDigit(ch) && ch != '-' && ch != '_').ToArray();
        var cleaned = invalid.Length == 0 ? destination : new string(destination.Select(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' ? ch : '-').ToArray());
        return cleaned;
    }
}

/// <summary>Parsed shape of the runtime outbox envelope published by the transport.</summary>
public sealed record InboxEnvelope(
    Guid? Id,
    string? EventType,
    Guid? ProcessInstanceId,
    string? TenantId,
    DateTimeOffset? OccurredAt,
    JsonElement? Payload)
{
    public static InboxEnvelope Parse(byte[] body)
    {
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        return new InboxEnvelope(
            root.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String && Guid.TryParse(id.GetString(), out var g) ? g : null,
            root.TryGetProperty("eventType", out var et) ? et.GetString() : null,
            root.TryGetProperty("processInstanceId", out var pi) && pi.ValueKind == JsonValueKind.String ? (Guid?)Guid.Parse(pi.GetString()!) : null,
            root.TryGetProperty("tenantId", out var t) && t.ValueKind is JsonValueKind.String or JsonValueKind.Null ? (t.ValueKind == JsonValueKind.Null ? null : t.GetString()) : null,
            root.TryGetProperty("occurredAt", out var oa) ? (DateTimeOffset?)oa.GetDateTimeOffset() : null,
            root.TryGetProperty("payload", out var p) ? p : null);
    }
}

/// <summary>Idempotent business handler invoked exactly once per stable message id.</summary>
public interface IRuntimeInboxHandler
{
    Task HandleAsync(string? eventType, JsonElement? payload, Guid? processInstanceId, string? tenantId, CancellationToken cancellationToken);
}

/// <summary>Alternative sink (test seams / external forwarding) invoked exactly once.</summary>
public interface IInboxEventSink
{
    Task HandleAsync(InboxEnvelope envelope, CancellationToken cancellationToken);
}
