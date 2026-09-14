using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Http.Json;
using VertexBPMN.AgentWorker;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.Tests.Unit.AgentWorker;

public sealed class ExternalTaskWorkerServiceTests
{
    [Fact]
    public async Task IndependentHeartbeatCancelsLongWorkWhenLeaseIsLost()
    {
        var lease = Lease();
        var handler = new BlockingHandler();
        var api = new LeaseLosingApi(lease, () => handler.IsRunning);
        var service = new ExternalTaskWorkerService(api, [handler], Microsoft.Extensions.Options.Options.Create(Options()),
            NullLogger<ExternalTaskWorkerService>.Instance);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(8));

        await service.StartAsync(timeout.Token);
        await handler.Started.Task.WaitAsync(timeout.Token);
        await api.HeartbeatCalled.Task.WaitAsync(timeout.Token);
        await handler.Cancelled.Task.WaitAsync(timeout.Token);
        await service.StopAsync(timeout.Token);

        Assert.Equal(1, api.MaximumClaimBatch);
        Assert.Equal(1, api.HeartbeatCount);
        Assert.True(api.WasRunningWhenHeartbeatArrived);
    }

    [Fact]
    public void EnabledWorkerRejectsUnsafeOrUnboundedConfiguration()
    {
        var validator = new ExternalTaskWorkerOptionsValidator();
        var valid = Options();
        valid.BaseAddress = "http://not-loopback.example";
        Assert.False(validator.Validate(null, valid).Succeeded);
        valid = Options();
        valid.MaxConcurrency = 33;
        Assert.False(validator.Validate(null, valid).Succeeded);
        valid = Options();
        valid.HeartbeatSeconds = 30;
        Assert.False(validator.Validate(null, valid).Succeeded);
        Assert.True(validator.Validate(null, Options()).Succeeded);
    }

    [Fact]
    public async Task TransportFailureDoesNotInvokeBusinessHandler()
    {
        var api = new FailingApi();
        var handler = new BlockingHandler();
        var settings = Options();
        settings.MaximumBackoffSeconds = 1;
        var service = new ExternalTaskWorkerService(api, [handler], Microsoft.Extensions.Options.Options.Create(settings),
            NullLogger<ExternalTaskWorkerService>.Instance);
        using var timeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));

        await service.StartAsync(timeout.Token);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Task.Delay(TimeSpan.FromSeconds(2), timeout.Token));
        await service.StopAsync(CancellationToken.None);

        Assert.True(api.Claims > 0);
        Assert.False(handler.Started.Task.IsCompleted);
    }

    [Fact]
    public async Task HttpClientUsesRenewedLeaseExpiryFromHeartbeatResponse()
    {
        var lease = Lease();
        var renewed = lease.LeaseExpiresAt + 10_000;
        var factory = new StubHttpClientFactory(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new ExternalTaskHeartbeatResult(
                lease.JobId, lease.LeaseGeneration, renewed, renewed - 1_000))
        });
        var client = new HttpExternalTaskApiClient(factory, new StaticTokenProvider(),
            Microsoft.Extensions.Options.Options.Create(Options()));

        var result = await client.HeartbeatAsync(lease, 10, TestContext.Current.CancellationToken);

        Assert.Equal(ExternalTaskHeartbeatOutcome.Accepted, result.Outcome);
        Assert.Equal(renewed, result.LeaseExpiresAt);
        Assert.Equal("Bearer", factory.LastRequest?.Headers.Authorization?.Scheme);
    }

    [Fact]
    public async Task HttpClientRejectsMalformedSuccessfulResponseAsTransportFailure()
    {
        var factory = new StubHttpClientFactory(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("not-json")
        });
        var client = new HttpExternalTaskApiClient(factory, new StaticTokenProvider(),
            Microsoft.Extensions.Options.Options.Create(Options()));

        await Assert.ThrowsAsync<ExternalTaskTransportException>(async () =>
            await client.ClaimAsync(["test.work"], 1, 10, TestContext.Current.CancellationToken));
    }

    private static ExternalTaskWorkerOptions Options() => new()
    {
        Enabled = true, BaseAddress = "http://127.0.0.1:5000/", TokenEndpoint = "http://127.0.0.1:5001/token",
        ClientId = "worker", ClientSecret = "secret", Topics = ["test.work"], MaxConcurrency = 1,
        MaxTasksPerClaim = 1, LeaseSeconds = 10, HeartbeatSeconds = 1, PollMilliseconds = 100,
        MaximumBackoffSeconds = 2
    };

    private static ExternalTaskLease Lease()
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        return new ExternalTaskLease(Guid.NewGuid(), Guid.NewGuid(), "test.work", "v1", null,
            System.Text.Json.JsonDocument.Parse("{}").RootElement.Clone(), "{}", Guid.NewGuid(), 1,
            now + 10_000, now + 30_000, 1, 1);
    }

    private sealed class LeaseLosingApi(ExternalTaskLease lease, Func<bool> isHandlerRunning) : IExternalTaskApiClient
    {
        private int _claims;
        public int HeartbeatCount { get; private set; }
        public int MaximumClaimBatch { get; private set; }
        public bool WasRunningWhenHeartbeatArrived { get; private set; }
        public TaskCompletionSource HeartbeatCalled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public ValueTask<IReadOnlyList<ExternalTaskLease>> ClaimAsync(IReadOnlyCollection<string> topics,
            int maxTasks, int leaseSeconds, CancellationToken cancellationToken)
        {
            MaximumClaimBatch = Math.Max(MaximumClaimBatch, maxTasks);
            return ValueTask.FromResult<IReadOnlyList<ExternalTaskLease>>(
                Interlocked.Increment(ref _claims) == 1 ? [lease] : []);
        }
        public ValueTask<ExternalTaskWorkerHeartbeatResult> HeartbeatAsync(ExternalTaskLease item, int leaseSeconds,
            CancellationToken cancellationToken)
        {
            HeartbeatCount++;
            WasRunningWhenHeartbeatArrived = isHandlerRunning();
            HeartbeatCalled.TrySetResult();
            return ValueTask.FromResult(new ExternalTaskWorkerHeartbeatResult(ExternalTaskHeartbeatOutcome.LeaseLost));
        }
    }

    private sealed class FailingApi : IExternalTaskApiClient
    {
        public int Claims { get; private set; }
        public ValueTask<IReadOnlyList<ExternalTaskLease>> ClaimAsync(IReadOnlyCollection<string> topics,
            int maxTasks, int leaseSeconds, CancellationToken cancellationToken)
        {
            Claims++;
            throw new ExternalTaskTransportException("unavailable");
        }
        public ValueTask<ExternalTaskWorkerHeartbeatResult> HeartbeatAsync(ExternalTaskLease lease, int leaseSeconds,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class BlockingHandler : IExternalTaskHandler
    {
        private volatile bool _running;
        public string Topic => "test.work";
        public bool IsRunning => _running;
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Cancelled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async ValueTask HandleAsync(ExternalTaskLease lease, CancellationToken cancellationToken)
        {
            _running = true;
            Started.TrySetResult();
            try { await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken); }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                Cancelled.TrySetResult();
                throw;
            }
            finally { _running = false; }
        }
    }

    private sealed class StaticTokenProvider : IWorkerAccessTokenProvider
    {
        public ValueTask<string> GetAccessTokenAsync(CancellationToken cancellationToken)
            => ValueTask.FromResult("test-token");
    }

    private sealed class StubHttpClientFactory(Func<HttpRequestMessage, HttpResponseMessage> response)
        : IHttpClientFactory
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        public HttpClient CreateClient(string name) => new(new Handler(this, response), disposeHandler: true);

        private sealed class Handler(
            StubHttpClientFactory owner,
            Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
                CancellationToken cancellationToken)
            {
                owner.LastRequest = request;
                return Task.FromResult(response(request));
            }
        }
    }
}
