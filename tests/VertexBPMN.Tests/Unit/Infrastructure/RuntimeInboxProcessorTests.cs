using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Infrastructure.Messaging;
using VertexBPMN.Infrastructure.Persistence;

namespace VertexBPMN.Tests.Unit.Infrastructure;

/// <summary>
/// Regression/behaviour tests for the provider-neutral <see cref="RuntimeInboxProcessor"/> (P2).
/// These encode the inbox-gap fixes: payload cloning, result classification, shared-transaction
/// completion, duplicate detection only on true unique-key conflicts, stale-claim recovery, and
/// refusal to fake success when no handler is registered.
/// </summary>
public sealed class RuntimeInboxProcessorTests
{
    private const string EventType = "test.event";

    // --- Shared harness: one in-memory SQLite connection reused by ever-changing scoped contexts ---

    private sealed class Harness : IAsyncDisposable
    {
        public SqliteConnection Connection { get; }
        public RuntimeInboxProcessor Processor { get; }
        public RecordingSink Sink { get; }
        public IServiceProvider Provider => ProviderImpl;
        private ServiceProvider ProviderImpl { get; }

        public Harness(int claimTimeoutSeconds = 120, bool registerHandler = true)
        {
            Connection = new SqliteConnection("Data Source=:memory:");
            Connection.Open();
            var services = new ServiceCollection();
            services.AddDbContext<BpmnDbContext>(o => o.UseSqlite(Connection));
            if (registerHandler)
            {
                Sink = new RecordingSink();
                services.AddSingleton<IInboxEventSink>(Sink);
            }
            else
            {
                Sink = null!;
            }
            ProviderImpl = services.BuildServiceProvider();

            // Create schema (including the unique inbox index) once on the shared connection.
            using (var db = Provider.CreateScope().ServiceProvider.GetRequiredService<BpmnDbContext>())
                db.Database.EnsureCreated();

            var inboxOptions = new RuntimeInboxOptions { ClaimTimeoutSeconds = claimTimeoutSeconds };
            Processor = new RuntimeInboxProcessor(
                Provider.GetRequiredService<IServiceScopeFactory>(),
                inboxOptions,
                NullLogger<RuntimeInboxProcessor>.Instance);
        }

        public async ValueTask DisposeAsync()
        {
            await ProviderImpl.DisposeAsync();
            await Connection.DisposeAsync();
        }

        public async Task<List<RuntimeInboxMessage>> ReadClaimsAsync(string idempotencyKey)
        {
            await using var db = new BpmnDbContext(new DbContextOptionsBuilder<BpmnDbContext>().UseSqlite(Connection).Options);
            return await db.RuntimeInbox
                .Where(m => m.IdempotencyKey == idempotencyKey)
                .OrderBy(m => m.ReceivedAt)
                .ToListAsync();
        }

        public async Task SeedIncompleteClaimAsync(string idempotencyKey, TimeSpan? aged)
        {
            await using var db = new BpmnDbContext(new DbContextOptionsBuilder<BpmnDbContext>().UseSqlite(Connection).Options);
            db.RuntimeInbox.Add(new RuntimeInboxMessage
            {
                Id = Guid.NewGuid(),
                Operation = EventType,
                IdempotencyKey = idempotencyKey,
                TenantId = "tenant-a",
                TenantScope = "tenant-a",
                ReceivedAt = DateTime.UtcNow - (aged ?? TimeSpan.Zero),
                CompletedAt = null
            });
            await db.SaveChangesAsync();
        }
    }

    private sealed class RecordingSink : IInboxEventSink
    {
        public int Handled { get; private set; }
        public Exception? ThrowOnHandle { get; set; }
        public List<InboxEnvelope> Received { get; } = new();

        public Task HandleAsync(InboxEnvelope envelope, CancellationToken cancellationToken)
        {
            if (ThrowOnHandle is not null)
                throw ThrowOnHandle;
            Handled++;
            Received.Add(envelope);
            return Task.CompletedTask;
        }
    }

    private static InboxEnvelope Envelope(string? eventType = EventType, Guid? id = null, string payloadJson = "{\"k\":\"v\"}")
    {
        var payload = JsonDocument.Parse(payloadJson).RootElement.Clone();
        return new InboxEnvelope(id ?? Guid.NewGuid(), eventType, Guid.NewGuid(), "tenant-a", DateTimeOffset.UtcNow, payload);
    }

    private static InboxEnvelope EnvelopeFromWireBytes(Guid id)
    {
        var json = JsonSerializer.Serialize(new
        {
            id = id.ToString(),
            eventType = EventType,
            processInstanceId = Guid.NewGuid().ToString(),
            tenantId = "tenant-a",
            occurredAt = DateTimeOffset.UtcNow,
            payload = new { order = "IN-1", amount = 7.25 }
        });
        return InboxEnvelope.Parse(Encoding.UTF8.GetBytes(json));
    }

    // --- Tests ---

    [Fact]
    public void ParsedPayload_IsCloned_AndOutlivesSourceDocument()
    {
        // Regression: JsonElement returned by Parse used to reference the disposed JsonDocument,
        // throwing ObjectDisposedException on access. It must now be an independent clone.
        var envelope = EnvelopeFromWireBytes(Guid.NewGuid());
        var payload = envelope.Payload!.Value;
        // Access properties after Parse's internal `using var document` has been disposed.
        var order = payload.GetProperty("order").GetString();
        var amount = payload.GetProperty("amount").GetDouble();
        Assert.Equal("IN-1", order);
        Assert.Equal(7.25, amount);
    }

    [Fact]
    public async Task Completed_InvokesHandlerAndCommitsCompletionMarker()
    {
        await using var h = new Harness();
        var key = Guid.NewGuid().ToString("N");
        var result = await h.Processor.ProcessAsync(Envelope(id: Guid.NewGuid()), key, TestContext.Current.CancellationToken);

        Assert.Equal(RuntimeInboxOutcome.Completed, result.Outcome);
        Assert.Equal(1, h.Sink.Handled);
        var claims = await h.ReadClaimsAsync(key);
        var claim = Assert.Single(claims);
        Assert.NotNull(claim.CompletedAt);
        Assert.Equal("Processed", claim.Result);
    }

    [Fact]
    public async Task CompletedDuplicate_DoesNotReinvokeHandler()
    {
        await using var h = new Harness();
        var key = Guid.NewGuid().ToString("N");
        var messageId = Guid.NewGuid();

        var first = await h.Processor.ProcessAsync(Envelope(id: messageId), key, TestContext.Current.CancellationToken);
        var second = await h.Processor.ProcessAsync(Envelope(id: messageId), key, TestContext.Current.CancellationToken);

        Assert.Equal(RuntimeInboxOutcome.Completed, first.Outcome);
        Assert.Equal(RuntimeInboxOutcome.CompletedDuplicate, second.Outcome);
        Assert.Equal(1, h.Sink.Handled); // business effect exactly once despite duplicate delivery
    }

    [Fact]
    public async Task Busy_WhenClaimHeldButNotExpired_SkipsWithoutHandler()
    {
        await using var h = new Harness(claimTimeoutSeconds: 120);
        var key = Guid.NewGuid().ToString("N");
        // Another (live, fresh) worker already holds an incomplete claim for this key.
        await h.SeedIncompleteClaimAsync(key, aged: TimeSpan.FromSeconds(10));

        var result = await h.Processor.ProcessAsync(Envelope(id: Guid.NewGuid()), key, TestContext.Current.CancellationToken);

        Assert.Equal(RuntimeInboxOutcome.Busy, result.Outcome);
        Assert.Equal(0, h.Sink.Handled);
    }

    [Fact]
    public async Task StaleIncompleteClaim_IsReclaimed_AndProcessed()
    {
        await using var h = new Harness(claimTimeoutSeconds: 60);
        var key = Guid.NewGuid().ToString("N");
        // A crash left an incomplete claim older than the claim timeout -> must become reclaimable.
        await h.SeedIncompleteClaimAsync(key, aged: TimeSpan.FromSeconds(300));

        var result = await h.Processor.ProcessAsync(Envelope(id: Guid.NewGuid()), key, TestContext.Current.CancellationToken);

        Assert.Equal(RuntimeInboxOutcome.Completed, result.Outcome);
        Assert.Equal(1, h.Sink.Handled);
        var claims = await h.ReadClaimsAsync(key);
        // After reclaim only a single completed row remains (the stale one was removed).
        Assert.Single(claims);
        Assert.NotNull(claims[0].CompletedAt);
    }

    [Fact]
    public async Task MissingHandler_IsRejected_NotFakeSuccess()
    {
        await using var h = new Harness(registerHandler: false);
        var result = await h.Processor.ProcessAsync(
            Envelope(id: Guid.NewGuid()), Guid.NewGuid().ToString("N"), TestContext.Current.CancellationToken);

        Assert.Equal(RuntimeInboxOutcome.Rejected, result.Outcome);
        Assert.Contains("No IRuntimeInboxHandler", result.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RetryableFailure_OnTransientHandlerError_DoesNotPersistCompletion()
    {
        await using var h = new Harness();
        h.Sink.ThrowOnHandle = new InvalidOperationException("remote timeout");
        var key = Guid.NewGuid().ToString("N");

        var result = await h.Processor.ProcessAsync(Envelope(id: Guid.NewGuid()), key, TestContext.Current.CancellationToken);

        Assert.Equal(RuntimeInboxOutcome.RetryableFailure, result.Outcome);
        // The completion marker must NOT be committed; the claim stays incomplete and reclaimable.
        var claims = await h.ReadClaimsAsync(key);
        var claim = Assert.Single(claims);
        Assert.Null(claim.CompletedAt);
        Assert.Null(claim.Result);
    }

    [Fact]
    public async Task HandlerRejectException_IsRejected()
    {
        await using var h = new Harness();
        h.Sink.ThrowOnHandle = new RuntimeInboxRejectException("unauthorized tenant mapping");
        var key = Guid.NewGuid().ToString("N");

        var result = await h.Processor.ProcessAsync(Envelope(id: Guid.NewGuid()), key, TestContext.Current.CancellationToken);

        Assert.Equal(RuntimeInboxOutcome.Rejected, result.Outcome);
        Assert.Contains("unauthorized tenant", result.Reason, StringComparison.Ordinal);
        var claims = await h.ReadClaimsAsync(key);
        Assert.Null(Assert.Single(claims).CompletedAt);
    }

    [Fact]
    public async Task MissingEventType_IsRejected()
    {
        await using var h = new Harness();
        var result = await h.Processor.ProcessAsync(
            Envelope(eventType: null), Guid.NewGuid().ToString("N"), TestContext.Current.CancellationToken);
        Assert.Equal(RuntimeInboxOutcome.Rejected, result.Outcome);
    }

    [Fact]
    public async Task MissingPayload_IsRejected()
    {
        await using var h = new Harness();
        var envelope = new InboxEnvelope(Guid.NewGuid(), EventType, Guid.NewGuid(), "tenant-a", DateTimeOffset.UtcNow, null);
        var result = await h.Processor.ProcessAsync(envelope, Guid.NewGuid().ToString("N"), TestContext.Current.CancellationToken);
        Assert.Equal(RuntimeInboxOutcome.Rejected, result.Outcome);
        Assert.Contains("payload", result.Reason, StringComparison.OrdinalIgnoreCase);
    }
}
