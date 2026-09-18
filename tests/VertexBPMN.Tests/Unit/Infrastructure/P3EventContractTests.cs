using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Infrastructure.Messaging;
using VertexBPMN.Infrastructure.Persistence;

namespace VertexBPMN.Tests.Unit.Infrastructure;

/// <summary>
/// P3 – Ereignisverträge absichern: beweist, dass Service-/Ai-Task-Dispatch zuerst DUReALD
/// (DB-Outbox, stabiler Id) und erst danach über den Outbox-Publisher zum Broker geht.
/// Es wird KEIN direkter Broker-Dispatcher registriert (IMessageDispatcher -> PersistentMessageDispatcher).
/// </summary>
public sealed class P3EventContractTests
{
    // --- Shared harness: eine in-memory SQLite-Verbindung, geteilte scoped Contexts ---
    private sealed class Harness : IAsyncDisposable
    {
        public SqliteConnection Connection { get; }
        public IServiceProvider Provider { get; }
        private ServiceProvider ProviderImpl { get; }

        public Harness()
        {
            Connection = new SqliteConnection("Data Source=:memory:");
            Connection.Open();
            var services = new ServiceCollection();
            services.AddDbContext<BpmnDbContext>(o => o.UseSqlite(Connection));
            services.AddScoped<IMessageDispatcher, PersistentMessageDispatcher>();
            ProviderImpl = services.BuildServiceProvider();
            Provider = ProviderImpl;
            using var db = ProviderImpl.CreateScope().ServiceProvider.GetRequiredService<BpmnDbContext>();
            db.Database.EnsureCreated();
        }

        public async ValueTask DisposeAsync()
        {
            await ProviderImpl.DisposeAsync();
            await Connection.DisposeAsync();
        }

        public async Task<List<RuntimeOutboxMessage>> ReadOutboxAsync()
        {
            await using var db = new BpmnDbContext(
                new DbContextOptionsBuilder<BpmnDbContext>().UseSqlite(Connection).Options);
            return await db.RuntimeOutbox.AsNoTracking().ToListAsync();
        }
    }

    private sealed class RecordingTransport : IRuntimeOutboxTransport
    {
        public List<KeyValuePair<string, string>> Sent { get; } = new();
        public ValueTask PublishAsync(RuntimeOutboxMessage message, CancellationToken ct = default)
        {
            Sent.Add(new KeyValuePair<string, string>(message.Id.ToString("N"), message.EventType));
            return ValueTask.CompletedTask;
        }
        public ValueTask<OutboxTransportHealth> CheckHealthAsync(CancellationToken ct = default) =>
            ValueTask.FromResult(new OutboxTransportHealth(true, "recording transport"));
    }

    [Fact]
    public async Task ServiceTaskDispatch_WritesDurableOutboxRow_ThenPublisherToBroker()
    {
        await using var h = new Harness();
        using var scope = h.Provider.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IMessageDispatcher>();
        var attributes = new Dictionary<string, string>
        {
            ["implementation"] = "calculateScore",
            ["vertex:mode"] = "verify"
        };
        var variables = new Dictionary<string, object> { ["applicantName"] = "Yova", ["age"] = 40 };

        await dispatcher.DispatchServiceTaskAsync("worker-1", "calculateScore", attributes, variables);

        // 1) Durable write happens first: exactly one Pending outbox row, no broker send yet.
        var pending = await h.ReadOutboxAsync();
        var row = Assert.Single(pending);
        Assert.Equal("ServiceTaskDispatch", row.EventType);
        Assert.Equal("Pending", row.State);
        Assert.NotEqual(Guid.Empty, row.Id);

        using var doc = JsonDocument.Parse(row.Payload);
        var root = doc.RootElement;
        Assert.Equal("worker-1", root.GetProperty("targetWorkerId").GetString());
        Assert.Equal("calculateScore", root.GetProperty("implementation").GetString());
        Assert.Equal("verify", root.GetProperty("attributes").GetProperty("vertex:mode").GetString());
        Assert.Equal("Yova", root.GetProperty("variables").GetProperty("applicantName").GetString());

        // 2) Broker send is a SEPARATE, durable outbox-publisher step with the SAME stable id.
        var transport = new RecordingTransport();
        var publisher = new RuntimeOutboxPublisherService(
            h.Provider.GetRequiredService<IServiceScopeFactory>(),
            transport,
            new RuntimeOutboxOptions { LeaseSeconds = 30 },
            NullLogger<RuntimeOutboxPublisherService>.Instance);
        var published = await publisher.RunOnceAsync();
        Assert.Equal(1, published);

        var sent = Assert.Single(transport.Sent);
        Assert.Equal(row.Id.ToString("N"), sent.Key); // MessageId == Outbox-Id
        Assert.Equal("ServiceTaskDispatch", sent.Value);

        var after = await h.ReadOutboxAsync();
        Assert.Equal("Published", Assert.Single(after).State);
    }

    [Fact]
    public async Task AiTaskDispatch_WritesDurableOutboxRow_ThenPublisherToBroker()
    {
        await using var h = new Harness();
        using var scope = h.Provider.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IMessageDispatcher>();
        var attributes = new Dictionary<string, string> { ["implementation"] = "classify" };
        var variables = new Dictionary<string, object> { ["text"] = "hello" };

        await dispatcher.DispatchAiTaskAsync("ai-worker", "openai", "gpt-4o-mini", attributes, variables);

        var row = Assert.Single(await h.ReadOutboxAsync());
        Assert.Equal("AiTaskDispatch", row.EventType);
        Assert.Equal("Pending", row.State);

        using var doc = JsonDocument.Parse(row.Payload);
        var root = doc.RootElement;
        Assert.Equal("ai-worker", root.GetProperty("targetWorkerId").GetString());
        Assert.Equal("openai", root.GetProperty("aiProvider").GetString());
        Assert.Equal("gpt-4o-mini", root.GetProperty("aiModel").GetString());
        Assert.Equal("hello", root.GetProperty("variables").GetProperty("text").GetString());

        var transport = new RecordingTransport();
        var publisher = new RuntimeOutboxPublisherService(
            h.Provider.GetRequiredService<IServiceScopeFactory>(),
            transport,
            new RuntimeOutboxOptions { LeaseSeconds = 30 },
            NullLogger<RuntimeOutboxPublisherService>.Instance);
        var published = await publisher.RunOnceAsync();
        Assert.Equal(1, published);
        Assert.Equal(row.Id.ToString("N"), Assert.Single(transport.Sent).Key);
        Assert.Equal("AiTaskDispatch", Assert.Single(transport.Sent).Value);
    }

    [Fact]
    public async Task Dispatcher_IsDurable_NoBrokerDependency_AllMethodsWritePendingOutbox()
    {
        // Production registriert PersistentMessageDispatcher als IMessageDispatcher (kein direkter
        // Broker-Dispatcher). Es ist KEIN IRuntimeOutboxTransport/ServiceBusClient registriert (Harness
        // ohne Broker), trotzdem duerf JEDE dauerhafte Dispatcher-Methode eine Pending-Outbox-Row anlegen:
        // die Dauerhaftigkeit haengt nicht am Broker.
        await using var h = new Harness();
        using var scope = h.Provider.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IMessageDispatcher>();
        Assert.IsType<PersistentMessageDispatcher>(dispatcher);

        var vars = new Dictionary<string, object> { ["k"] = "v" };
        var attrs = new Dictionary<string, string> { ["implementation"] = "x" };

        await dispatcher.DispatchUserTaskAsync("bob", "task-1", vars);
        await dispatcher.QueueTaskAsync("task-2", "type-a", vars);
        await dispatcher.DispatchServiceTaskAsync("w", "x", attrs, vars);
        await dispatcher.DispatchAiTaskAsync("w", "provider", "model", attrs, vars);
        await dispatcher.PublishCaseTokenAsync(new Domain.Model.Cmn.CaseToken(
            Guid.NewGuid(), Guid.Empty, "", "",
            new Dictionary<string, object>(), DateTime.UtcNow));

        var rows = await h.ReadOutboxAsync();
        Assert.Equal(5, rows.Count);
        Assert.All(rows, r => Assert.Equal("Pending", r.State));
        Assert.Contains(rows, r => r.EventType == "UserTaskDispatch");
        Assert.Contains(rows, r => r.EventType == "TaskQueued");
        Assert.Contains(rows, r => r.EventType == "ServiceTaskDispatch");
        Assert.Contains(rows, r => r.EventType == "AiTaskDispatch");
        Assert.Contains(rows, r => r.EventType == "CaseTokenPublished");
        // Keine der Rows wurde bei Aufruf bereits an einen Broker versendet.
        Assert.All(rows, r => Assert.Null(r.PublishedAt));
    }
}
