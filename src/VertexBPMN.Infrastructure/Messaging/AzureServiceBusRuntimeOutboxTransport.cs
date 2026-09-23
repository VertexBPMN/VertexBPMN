using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Azure.Core;
using Azure.Identity;
using Azure.Messaging.ServiceBus;
using VertexBPMN.Domain.Entities;

namespace VertexBPMN.Infrastructure.Messaging;

/// <summary>
/// Durable runtime-outbox transport for Azure Service Bus.
///
/// The JSON envelope matches RabbitMQ/Kafka exactly so downstream inbox consumers see the same
/// shape regardless of provider. The stable MessageId is the outbox id ("N" format, like RabbitMQ),
/// so consumers can de-duplicate retries; correlation/tenant/event-type metadata travel as
/// Application Properties.
///
/// Authentication is explicit and has NO unintended fallback chain in production:
///  - ManagedIdentity  -> uses ManagedIdentityCredential (optionally a User-Assigned client id).
///  - ConnectionString -> only intended for local integration tests.
/// </summary>
public sealed class AzureServiceBusRuntimeOutboxTransport : IRuntimeOutboxTransport, IAsyncDisposable
{
    private readonly RuntimeOutboxOptions _options;
    private readonly object _gate = new();
    private ServiceBusClient? _client;
    private ServiceBusSender? _sender;

    public AzureServiceBusRuntimeOutboxTransport(RuntimeOutboxOptions options)
    {
        _options = options;
        ValidateOptions(options);
    }

    private static void ValidateOptions(RuntimeOutboxOptions options)
    {
        if (options.AuthenticationMode == RuntimeOutboxAuthenticationMode.ManagedIdentity
            && string.IsNullOrWhiteSpace(options.FullyQualifiedNamespace))
        {
            throw new InvalidOperationException(
                "Runtime:Outbox:FullyQualifiedNamespace is required when using Azure Service Bus with Managed Identity.");
        }

        if (string.IsNullOrWhiteSpace(options.EntityName))
            throw new InvalidOperationException("Runtime:Outbox:EntityName (topic or queue name) is required for Azure Service Bus.");

        if (options.AuthenticationMode == RuntimeOutboxAuthenticationMode.ConnectionString
            && string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            throw new InvalidOperationException(
                "Runtime:Outbox:ConnectionString is required when AuthenticationMode=ConnectionString (local integration tests).");
        }
    }

    private ServiceBusSender GetSender()
    {
        var sender = _sender;
        if (sender is not null)
            return sender;

        lock (_gate)
        {
            if (_sender is null)
            {
                _client = CreateClient();
                _sender = _client.CreateSender(_options.EntityName!);
            }
            return _sender;
        }
    }

    private ServiceBusClient CreateClient()
    {
        if (_options.AuthenticationMode == RuntimeOutboxAuthenticationMode.ManagedIdentity)
        {
            TokenCredential credential = string.IsNullOrWhiteSpace(_options.ManagedIdentityClientId)
                ? new ManagedIdentityCredential()
                : new ManagedIdentityCredential(_options.ManagedIdentityClientId);
            return new ServiceBusClient(_options.FullyQualifiedNamespace!, credential);
        }

        return new ServiceBusClient(_options.ConnectionString!);
    }

    public async ValueTask PublishAsync(RuntimeOutboxMessage message, CancellationToken cancellationToken = default)
    {
        var serviceBusMessage = BuildServiceBusMessage(message);

        var timeoutSeconds = Math.Clamp(_options.OperationTimeoutSeconds, 1, 600);
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

        try
        {
            await GetSender().SendMessageAsync(serviceBusMessage, timeoutCts.Token).ConfigureAwait(false);
        }
        catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.MessageSizeExceeded)
        {
            // Nontransient, diagnostically clear: give the publisher a concrete reason in LastError.
            throw new InvalidOperationException(
                $"Azure Service Bus message size limit exceeded for outbox message {message.Id}. " +
                $"Inspect payload size including application properties.", ex);
        }
    }

    /// <summary>
    /// Pure mapping from a runtime-outbox record to the wire format (P8.1 contract).
    /// The JSON envelope matches RabbitMQ/Kafka exactly so downstream inbox consumers see the same
    /// shape regardless of provider. The stable MessageId is the outbox id ("N" format), enabling
    /// de-duplication across retries; correlation/tenant/event-type/posted metadata travel as
    /// Application Properties. Extracted so the contract is unit-testable without a live broker.
    /// </summary>
    internal static ServiceBusMessage BuildServiceBusMessage(RuntimeOutboxMessage message)
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

        var serviceBusMessage = new ServiceBusMessage(Encoding.UTF8.GetBytes(envelope))
        {
            MessageId = message.Id.ToString("N"),
            CorrelationId = message.ProcessInstanceId == Guid.Empty ? null : message.ProcessInstanceId.ToString("N"),
            ContentType = "application/json",
            Subject = message.EventType
        };
        serviceBusMessage.ApplicationProperties["eventType"] = message.EventType;
        serviceBusMessage.ApplicationProperties["tenantId"] = message.TenantId ?? string.Empty;
        serviceBusMessage.ApplicationProperties["processInstanceId"] = message.ProcessInstanceId.ToString("N");

        return serviceBusMessage;
    }

    /// <summary>
    /// Connection/operational indicator: verifies the configured namespace is reachable on the AMQP
    /// TLS endpoint (5671) within a bounded window. This intentionally requires NO management rights and
    /// produces no side effect. A full send/receive end-to-end probe is the separate Stage acceptance
    /// check and is not part of this health indicator.
    /// </summary>
    public async ValueTask<OutboxTransportHealth> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var host = ResolveNamespaceHost();
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(10));
            using var tcp = new TcpClient();
            await tcp.ConnectAsync(host, 5671, timeoutCts.Token).ConfigureAwait(false);
            return new OutboxTransportHealth(true, $"Azure Service Bus namespace reachable ({host}:5671).");
        }
        catch (Exception ex)
        {
            return new OutboxTransportHealth(false, $"Azure Service Bus unreachable: {ex.Message}");
        }
    }

    private string ResolveNamespaceHost()
    {
        if (_options.AuthenticationMode == RuntimeOutboxAuthenticationMode.ManagedIdentity)
            return _options.FullyQualifiedNamespace!;

        // Parse from "Endpoint=sb://<host>/;SharedAccessKeyName=...;SharedAccessKey=..."
        var endpoint = _options.ConnectionString!
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .First(part => part.StartsWith("Endpoint=", StringComparison.OrdinalIgnoreCase))
            .Substring("Endpoint=".Length);
        return new Uri(endpoint.StartsWith("sb://", StringComparison.OrdinalIgnoreCase) ? endpoint : "sb://" + endpoint).Host;
    }

    public async ValueTask DisposeAsync()
    {
        ServiceBusSender? sender;
        ServiceBusClient? client;
        lock (_gate)
        {
            sender = _sender;
            client = _client;
            _sender = null;
            _client = null;
        }

        if (sender is not null)
            await sender.DisposeAsync().ConfigureAwait(false);
        if (client is not null)
            await client.DisposeAsync().ConfigureAwait(false);
    }
}
