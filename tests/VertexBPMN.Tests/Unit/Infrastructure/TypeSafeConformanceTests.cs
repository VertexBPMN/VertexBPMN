using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using VertexBPMN.Infrastructure.Messaging;
using VertexBPMN.Infrastructure.Persistence;
using Xunit;

namespace VertexBPMN.Tests.Unit.Infrastructure;

/// <summary>
/// P3: TypeSafe-Envelope-Konformitäts-Judgment. Unit-Tests der Verdict-Mapping-Logik (Fake-Client,
/// kein Netz) plus Prozessor-Integration (config-gated: unveränderter Pfad bei NotEvaluated,
/// Rejected bei Malformed/WrongContract, Retryable bei Unclear).
/// </summary>
public sealed class TypeSafeConformanceTests
{
    // ---------- Validator unit tests ----------

    private static InboxEnvelope Envelope(string eventType = "ServiceTaskDispatch",
        string payloadJson = "{\"targetWorkerId\":\"w\",\"implementation\":\"f\"}")
    {
        using var doc = JsonDocument.Parse(payloadJson);
        return new InboxEnvelope(Guid.NewGuid(), eventType, Guid.NewGuid(), "tenant-a",
            DateTimeOffset.UtcNow, doc.RootElement.Clone());
    }

    private sealed class FakeClient : ITypeSafeConformanceClient
    {
        public TypeSafeJudgmentAnswer? Answer { get; set; }
        public Task<TypeSafeJudgmentAnswer?> AskConformanceAsync(string stateJson, CancellationToken ct)
            => Task.FromResult(Answer);
    }

    private static TypeSafeEnvelopeConformanceValidator Validator(FakeClient client, bool enabled = true,
        double threshold = 0.6)
    {
        var options = new TypeSafeConformanceOptions
        {
            Enabled = enabled,
            ConfidenceThreshold = threshold,
            ApiKey = "test-key"
        };
        return new TypeSafeEnvelopeConformanceValidator(client, options,
            NullLogger<TypeSafeEnvelopeConformanceValidator>.Instance);
    }

    private static TypeSafeJudgmentAnswer Choice(string choice, double confidence) =>
        new(choice, confidence, new Dictionary<string, double> { [choice] = confidence });

    [Theory]
    [InlineData("conforms", EnvelopeConformanceVerdict.Conforms)]
    [InlineData("malformed", EnvelopeConformanceVerdict.Malformed)]
    [InlineData("wrong_contract", EnvelopeConformanceVerdict.WrongContract)]
    public async Task Choice_Maps_To_Verdict(string choice, EnvelopeConformanceVerdict expected)
    {
        var client = new FakeClient { Answer = Choice(choice, 0.9) };
        var result = await Validator(client).EvaluateAsync(Envelope(), CancellationToken.None);
        Assert.Equal(expected, result.Verdict);
    }

    [Fact]
    public async Task Disabled_Returns_NotEvaluated_Without_Client_Call()
    {
        var client = new FakeClient { Answer = Choice("malformed", 0.9) };
        var result = await Validator(client, enabled: false).EvaluateAsync(Envelope(), CancellationToken.None);
        Assert.Equal(EnvelopeConformanceVerdict.NotEvaluated, result.Verdict);
    }

    [Fact]
    public async Task Low_Confidence_Maps_To_Unclear()
    {
        var client = new FakeClient { Answer = Choice("conforms", 0.4) };
        var result = await Validator(client).EvaluateAsync(Envelope(), CancellationToken.None);
        Assert.Equal(EnvelopeConformanceVerdict.Unclear, result.Verdict);
    }

    [Fact]
    public async Task Unreachable_Client_Fails_Open_To_NotEvaluated()
    {
        var client = new FakeClient { Answer = null };
        var result = await Validator(client).EvaluateAsync(Envelope(), CancellationToken.None);
        Assert.Equal(EnvelopeConformanceVerdict.NotEvaluated, result.Verdict);
    }

    [Fact]
    public async Task Unknown_Choice_Maps_To_Unclear()
    {
        var client = new FakeClient { Answer = Choice("bogus", 0.9) };
        var result = await Validator(client).EvaluateAsync(Envelope(), CancellationToken.None);
        Assert.Equal(EnvelopeConformanceVerdict.Unclear, result.Verdict);
    }

    // ---------- Processor integration ----------

    private sealed class FakeValidator : ITypeSafeEnvelopeConformanceValidator
    {
        public EnvelopeConformanceResult Result { get; set; }
        public Task<EnvelopeConformanceResult> EvaluateAsync(InboxEnvelope envelope, CancellationToken ct)
            => Task.FromResult(Result);
    }

    private sealed class RecordingSink : IInboxEventSink
    {
        public int Calls { get; private set; }
        public Task HandleAsync(InboxEnvelope envelope, CancellationToken ct) { Calls++; return Task.CompletedTask; }
    }

    private sealed class Harness : IAsyncDisposable
    {
        public SqliteConnection Connection { get; }
        public RuntimeInboxProcessor Processor { get; }
        public RecordingSink Sink { get; } = new();
        public FakeValidator Validator { get; } = new();
        private ServiceProvider Provider { get; }

        public Harness(EnvelopeConformanceResult? verdict = null)
        {
            Connection = new SqliteConnection("Data Source=:memory:");
            Connection.Open();
            var services = new ServiceCollection();
            services.AddDbContext<BpmnDbContext>(o => o.UseSqlite(Connection));
            services.AddSingleton<IInboxEventSink>(Sink);
            if (verdict is { } v)
            {
                Validator.Result = v;
                services.AddSingleton<ITypeSafeEnvelopeConformanceValidator>(Validator);
            }
            Provider = services.BuildServiceProvider();
            using (var db = Provider.CreateScope().ServiceProvider.GetRequiredService<BpmnDbContext>())
                db.Database.EnsureCreated();

            Processor = new RuntimeInboxProcessor(
                Provider.GetRequiredService<IServiceScopeFactory>(),
                new RuntimeInboxOptions(),
                NullLogger<RuntimeInboxProcessor>.Instance);
        }

        public async ValueTask DisposeAsync()
        {
            await Provider.DisposeAsync();
            await Connection.DisposeAsync();
        }
    }

    [Fact]
    public async Task No_Validator_Registered_Pipeline_Unchanged_Completed()
    {
        await using var h = new Harness(verdict: null);
        var result = await h.Processor.ProcessAsync(Envelope(), $"k-{Guid.NewGuid():N}", CancellationToken.None);
        Assert.Equal(RuntimeInboxOutcome.Completed, result.Outcome);
        Assert.Equal(1, h.Sink.Calls);
    }

    [Fact]
    public async Task Malformed_Verdict_Rejects_Before_Handler()
    {
        await using var h = new Harness(new EnvelopeConformanceResult(EnvelopeConformanceVerdict.Malformed, 0.95, "fehlt targetWorkerId"));
        var result = await h.Processor.ProcessAsync(Envelope(), $"k-{Guid.NewGuid():N}", CancellationToken.None);
        Assert.Equal(RuntimeInboxOutcome.Rejected, result.Outcome);
        Assert.Equal(0, h.Sink.Calls);
    }

    [Fact]
    public async Task WrongContract_Verdict_Rejects_Before_Handler()
    {
        await using var h = new Harness(new EnvelopeConformanceResult(EnvelopeConformanceVerdict.WrongContract, 0.9, "semantisch anderes Ereignis"));
        var result = await h.Processor.ProcessAsync(Envelope(), $"k-{Guid.NewGuid():N}", CancellationToken.None);
        Assert.Equal(RuntimeInboxOutcome.Rejected, result.Outcome);
        Assert.Equal(0, h.Sink.Calls);
    }

    [Fact]
    public async Task Unclear_Verdict_Is_Retryable_For_Review()
    {
        await using var h = new Harness(new EnvelopeConformanceResult(EnvelopeConformanceVerdict.Unclear, 0.45));
        var result = await h.Processor.ProcessAsync(Envelope(), $"k-{Guid.NewGuid():N}", CancellationToken.None);
        Assert.Equal(RuntimeInboxOutcome.RetryableFailure, result.Outcome);
        Assert.Equal(0, h.Sink.Calls);
    }

    [Fact]
    public async Task Conforms_Verdict_Proceeds_To_Handler_Completed()
    {
        await using var h = new Harness(new EnvelopeConformanceResult(EnvelopeConformanceVerdict.Conforms, 0.98));
        var result = await h.Processor.ProcessAsync(Envelope(), $"k-{Guid.NewGuid():N}", CancellationToken.None);
        Assert.Equal(RuntimeInboxOutcome.Completed, result.Outcome);
        Assert.Equal(1, h.Sink.Calls);
    }

    [Fact]
    public async Task NotEvaluated_Verdict_Proceeds_To_Handler_Completed()
    {
        await using var h = new Harness(new EnvelopeConformanceResult(EnvelopeConformanceVerdict.NotEvaluated));
        var result = await h.Processor.ProcessAsync(Envelope(), $"k-{Guid.NewGuid():N}", CancellationToken.None);
        Assert.Equal(RuntimeInboxOutcome.Completed, result.Outcome);
        Assert.Equal(1, h.Sink.Calls);
    }
}
