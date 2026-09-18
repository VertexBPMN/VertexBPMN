using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Infrastructure.Persistence;

namespace VertexBPMN.Infrastructure.Messaging;

/// <summary>
/// Outcome classifications of one idempotent inbox processing attempt.
/// Consumers (RabbitMQ, Azure Service Bus) map these onto their ack/requeue/reject semantics.
/// </summary>
public enum RuntimeInboxOutcome
{
    /// <summary>Business effect was applied and the completion marker committed atomically.</summary>
    Completed,
    /// <summary>The message was already completed exactly once (idempotent no-op). Safe to acknowledge.</summary>
    CompletedDuplicate,
    /// <summary>The message is owned by another live worker and its claim has not expired. Safe to skip (ack).</summary>
    Busy,
    /// <summary>Transient failure; the consumer should redeliver/requeue and retry.</summary>
    RetryableFailure,
    /// <summary>Permanent, non-retryable failure (missing handler, invalid payload, unknown contract version,
    /// unauthorized tenant). The consumer should move the message to the DLQ, not ack as success.</summary>
    Rejected
}

/// <summary>Outcome + an optional diagnostic reason (used for DLQ "dead-letter reason" and logs).</summary>
public readonly record struct RuntimeInboxProcessResult(RuntimeInboxOutcome Outcome, string? Reason = null);

/// <summary>
/// Thrown by a business handler to permanently reject a message (-> DLQ). Any other exception is treated
/// as a transient, retryable failure.
/// </summary>
public sealed class RuntimeInboxRejectException : Exception
{
    public RuntimeInboxRejectException(string reason, Exception? inner = null) : base(reason, inner) { }
}

/// <summary>
/// Provider-neutral, idempotent inbox processing shared by every transport consumer (RabbitMQ now,
/// Azure Service Bus via <see cref="AzureServiceBusRuntimeInboxConsumerService"/>).
///
/// Guarantees:
///  - The envelope payload is cloned from the source <see cref="JsonDocument"/> and outlives its disposal.
///  - A durable idempotency claim (unique <c>(TenantScope, Operation, IdempotencyKey)</c>) reserves the
///    message so concurrent / duplicate deliveries have exactly one winner.
///  - Only a real unique-key conflict is treated as a duplicate. Incomplete claims are reclaimed after a
///    configurable claim timeout (recovery after a crash mid-processing), otherwise reported as busy.
///  - The business effect and the completion marker are committed in one shared transaction, so a crash
///    after the business effect but before the completion marker cannot partially apply the effect
///    (commit-before-ack safety is delegated to the consumer's ack after this returns Completed).
///  - A missing handler, unparseable payload, unknown contract version or unauthorized tenant is reported
///    as <see cref="RuntimeInboxOutcome.Rejected"/>, never as a fake success.
/// </summary>
public sealed class RuntimeInboxProcessor
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly RuntimeInboxOptions _options;
    private readonly ILogger<RuntimeInboxProcessor> _logger;

    public RuntimeInboxProcessor(
        IServiceScopeFactory scopeFactory,
        RuntimeInboxOptions options,
        ILogger<RuntimeInboxProcessor> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    public async Task<RuntimeInboxProcessResult> ProcessAsync(
        InboxEnvelope envelope,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(envelope.EventType))
            return new(RuntimeInboxOutcome.Rejected, "Envelope has no eventType.");

        if (envelope.Payload is not { } payload)
            return new(RuntimeInboxOutcome.Rejected, "Envelope has no payload.");

        if (payload.ValueKind == JsonValueKind.Undefined || payload.ValueKind == JsonValueKind.Null)
            return new RuntimeInboxProcessResult(RuntimeInboxOutcome.Rejected,
                $"Envelope payload has invalid value kind '{payload.ValueKind}'.");

        var claimWindow = TimeSpan.FromSeconds(Math.Clamp(_options.ClaimTimeoutSeconds, 1, 86400));

        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BpmnDbContext>();
        var tenantId = envelope.TenantId;
        var tenantScope = string.IsNullOrWhiteSpace(tenantId) ? "$global" : tenantId.Trim();
        var operation = envelope.EventType;

        // 1) Reserve the idempotency claim. Unique (TenantScope, Operation, IdempotencyKey) commits this
        //    first so concurrent/duplicate deliveries observe exactly one winner.
        var claim = new RuntimeInboxMessage
        {
            Id = Guid.NewGuid(),
            Operation = operation,
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
            // True duplicate: the unique constraint rejected our insert. Expect exactly the unique-index
            // conflict; any other DbUpdateException falls through to retryable (DB transient errors).
            db.ChangeTracker.Clear();
            var existing = await db.RuntimeInbox.AsNoTracking().SingleOrDefaultAsync(
                m => m.Operation == operation
                     && m.IdempotencyKey == idempotencyKey
                     && m.TenantScope == tenantScope,
                cancellationToken);
            if (existing is null)
                return new(RuntimeInboxOutcome.RetryableFailure, "Duplicate insert detected but existing row not found; retry.");
            if (existing.CompletedAt is not null)
                return new(RuntimeInboxOutcome.CompletedDuplicate, "Already completed exactly once.");

            // Incomplete claim: either another live worker holds it (Busy) or it is stale and reclaimable
            // after the claim timeout (crash mid-processing -> recovery).
            if (existing.ReceivedAt >= DateTime.UtcNow - claimWindow)
                return new(RuntimeInboxOutcome.Busy, "Claimed by another worker and claim has not expired.");

            // Stale incomplete claim -> reclaim (delete old row, re-reserve) and reprocess below.
            await db.RuntimeInbox.Where(m => m.Id == existing.Id).ExecuteDeleteAsync(cancellationToken);
            db.ChangeTracker.Clear();
            db.RuntimeInbox.Add(claim);
            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                return new(RuntimeInboxOutcome.Busy, "Reclaim lost the race to another worker; skipping.");
            }
        }

        // 2) No business handler registered -> this is a permanent misconfiguration, NOT a success.
        var handler = scope.ServiceProvider.GetService<IRuntimeInboxHandler>();
        var sink = scope.ServiceProvider.GetService<IInboxEventSink>();
        if (handler is null && sink is null)
            return new(RuntimeInboxOutcome.Rejected,
                "No IRuntimeInboxHandler or IInboxEventSink is registered; refusing to acknowledge the message as processed.");

        // 3) Shared transaction: business effect + completion marker commit atomically.
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            if (handler is not null)
                await handler.HandleAsync(envelope.EventType, envelope.Payload, envelope.ProcessInstanceId, tenantId, cancellationToken);
            else
                await sink!.HandleAsync(envelope, cancellationToken);

            claim.Result = "Processed";
            claim.CompletedAt = DateTime.UtcNow;
            db.Attach(claim);
            db.Entry(claim).State = EntityState.Modified;
            await db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
            return new(RuntimeInboxOutcome.Completed);
        }
        catch (RuntimeInboxRejectException ex)
        {
            await tx.RollbackAsync(CancellationToken.None);
            return new(RuntimeInboxOutcome.Rejected, ex.Message);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await tx.RollbackAsync(CancellationToken.None);
            throw;
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(CancellationToken.None);
            _logger.LogWarning(ex, "Inbox message {Id} ({Event}) failed transiently; classified as retryable.",
                idempotencyKey, envelope.EventType);
            return new(RuntimeInboxOutcome.RetryableFailure, ex.Message);
        }
    }
}

/// <summary>
/// Parsed shape of the runtime outbox envelope published by the transport.
/// The payload is <see cref="JsonElement.Clone()"/>d so it outlives the source
/// <see cref="JsonDocument"/> used to parse the wire body.
/// </summary>
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
            root.TryGetProperty("payload", out var p) ? p.Clone() : null);
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

