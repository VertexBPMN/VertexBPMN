using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using VertexBPMN.Application.Connectors;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.Tests.Unit.Application;

public sealed class ConnectorRuntimeTests
{
    [Fact]
    public async Task Runtime_RetriesAndRedactsSensitiveOutputs()
    {
        var executor = new ScriptedExecutor(
            new ConnectorExecutionResult(false, 503, new Dictionary<string, object>(), "remote_server_error"),
            new ConnectorExecutionResult(true, 200, new Dictionary<string, object> { ["token"] = "clear-secret", ["status"] = "ok" }));
        var runtime = new ConnectorRuntime(new ConnectorRegistry([executor]), new ConnectorRateLimitPolicy(), new ConnectorRedactionPolicy(), NullLogger<ConnectorRuntime>.Instance);
        var result = await runtime.ExecuteAsync(Context(new ConnectorRetryPolicy(2, TimeSpan.FromSeconds(1), TimeSpan.Zero)));

        Assert.True(result.Success);
        Assert.Equal(2, result.Attempts);
        Assert.Equal("***", result.Outputs["token"]);
        Assert.Equal("ok", result.Outputs["status"]);
        Assert.Equal(2, executor.Calls);
    }

    [Fact]
    public async Task Handler_ResolvesCredentialOnlyForAllowedHost_AndAuditsWithoutSecret()
    {
        var secret = $"redaction-canary-{Guid.NewGuid():N}";
        var credentials = new Mock<ICredentialService>();
        credentials.Setup(x => x.ResolveSecretAsync("tenant-a", "credential-1", "token", It.IsAny<CancellationToken>())).ReturnsAsync(secret);
        AuditLog? capturedAudit = null;
        var audit = new Mock<IAuditLogService>();
        audit.Setup(x => x.RecordAsync(It.IsAny<AuditLog>(), It.IsAny<CancellationToken>())).Callback<AuditLog, CancellationToken>((entry, _) => capturedAudit = entry).ReturnsAsync((AuditLog entry, CancellationToken _) => entry);
        ConnectorExecutionContext? capturedContext = null;
        var runtime = new Mock<IConnectorRuntime>();
        runtime.Setup(x => x.ExecuteAsync(It.IsAny<ConnectorExecutionContext>(), It.IsAny<CancellationToken>())).Callback<ConnectorExecutionContext, CancellationToken>((context, _) => capturedContext = context).ReturnsAsync(new ConnectorExecutionResult(true, 200, new Dictionary<string, object>()));
        using var provider = new ServiceCollection().AddSingleton(credentials.Object).AddSingleton(audit.Object).BuildServiceProvider();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectorRuntime:AllowedCredentialHosts:0"] = "api.example.test" }).Build();
        var handler = new VertexConnectorServiceTaskHandler(runtime.Object, provider.GetRequiredService<IServiceScopeFactory>(), configuration);
        var attributes = new Dictionary<string, string>
        {
            ["vertex:connector.type"] = "http",
            ["vertex:connector.operationId"] = "http.request",
            ["vertex:connector.endpoint"] = "https://api.example.test/orders",
            ["vertex:connector.tenantId"] = "tenant-a",
            ["vertex:connector.credentialRef"] = "credential-1"
        };

        await handler.ExecuteAsync(attributes, new Dictionary<string, object>());

        Assert.Equal(secret, capturedContext!.CredentialSecret);
        Assert.NotNull(capturedAudit);
        Assert.DoesNotContain(secret, capturedAudit!.DetailsJson ?? string.Empty, StringComparison.Ordinal);
        Assert.Contains("connector.executed", capturedAudit.Action, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Handler_RejectsCredentialForHostOutsideAllowlistBeforeResolution()
    {
        var credentials = new Mock<ICredentialService>();
        using var provider = new ServiceCollection().AddSingleton(credentials.Object).BuildServiceProvider();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectorRuntime:AllowedCredentialHosts:0"] = "allowed.example.test" }).Build();
        var handler = new VertexConnectorServiceTaskHandler(Mock.Of<IConnectorRuntime>(), provider.GetRequiredService<IServiceScopeFactory>(), configuration);
        var attributes = new Dictionary<string, string>
        {
            ["vertex:connector.type"] = "http",
            ["vertex:connector.operationId"] = "http.request",
            ["vertex:connector.endpoint"] = "https://blocked.example.test/orders",
            ["vertex:connector.credentialRef"] = "credential-1"
        };

        await Assert.ThrowsAsync<VertexBPMN.Domain.Exceptions.ServiceTaskExecutionException>(() => handler.ExecuteAsync(attributes, new Dictionary<string, object>()));
        credentials.Verify(x => x.ResolveSecretAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handler_RejectsSmtpCredentialForHostOutsideAllowlistBeforeResolution()
    {
        var credentials = new Mock<ICredentialService>();
        using var provider = new ServiceCollection().AddSingleton(credentials.Object).BuildServiceProvider();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectorRuntime:AllowedCredentialHosts:0"] = "mail.example.test"
        }).Build();
        var handler = new VertexConnectorServiceTaskHandler(
            Mock.Of<IConnectorRuntime>(),
            provider.GetRequiredService<IServiceScopeFactory>(),
            configuration);
        var attributes = new Dictionary<string, string>
        {
            ["vertex:connector.type"] = "smtp",
            ["vertex:connector.operationId"] = "smtp.send",
            ["vertex:connector.smtpHost"] = "blocked.example.test",
            ["vertex:connector.credentialRef"] = "credential-1"
        };

        await Assert.ThrowsAsync<VertexBPMN.Domain.Exceptions.ServiceTaskExecutionException>(
            () => handler.ExecuteAsync(attributes, new Dictionary<string, object>()));
        credentials.Verify(
            x => x.ResolveSecretAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Runtime_MapsTimeoutWithoutLeakingExceptionDetails()
    {
        var executor = new DelayingExecutor();
        var runtime = new ConnectorRuntime(new ConnectorRegistry([executor]), new ConnectorRateLimitPolicy(), new ConnectorRedactionPolicy(), NullLogger<ConnectorRuntime>.Instance);
        var result = await runtime.ExecuteAsync(Context(new ConnectorRetryPolicy(1, TimeSpan.FromMilliseconds(20), TimeSpan.Zero)));
        Assert.False(result.Success);
        Assert.Equal("timeout", result.ErrorCode);
        Assert.Empty(result.Outputs);
    }

    [Fact]
    public async Task RateLimit_SpacesExecutionsForSameTenantTypeAndHost()
    {
        var executor = new ScriptedExecutor(
            new ConnectorExecutionResult(true, 200, new Dictionary<string, object>()),
            new ConnectorExecutionResult(true, 200, new Dictionary<string, object>()));
        var limiter = new ConnectorRateLimitPolicy();
        var runtime = new ConnectorRuntime(new ConnectorRegistry([executor]), limiter, new ConnectorRedactionPolicy(), NullLogger<ConnectorRuntime>.Instance);
        var context = new ConnectorExecutionContext("tenant-a", "test", "test.execute", new Uri("https://api.example.test"), new Dictionary<string, string> { ["vertex:connector.requestsPerSecond"] = "10" }, new Dictionary<string, object>(), new ConnectorRetryPolicy(1));
        await runtime.ExecuteAsync(context);
        var started = System.Diagnostics.Stopwatch.StartNew();
        await runtime.ExecuteAsync(context);
        Assert.True(started.Elapsed >= TimeSpan.FromMilliseconds(75));
    }

    [Fact]
    public void Registry_ResolvesEveryBuiltInConnectorType()
    {
        using var client = new HttpClient(new SuccessfulHandler());
        IConnectorExecutor[] executors =
        [
            new HttpConnectorExecutor(client), new NamedHttpConnectorExecutor(client, "webhook"), new NamedHttpConnectorExecutor(client, "slack"), new NamedHttpConnectorExecutor(client, "ai"),
            new DelayConnectorExecutor(), new EmailConnectorExecutor(), new NamedEmailConnectorExecutor("smtp"), new DatabaseConnectorExecutor(),
            new NamedDatabaseConnectorExecutor("db"), new NamedDatabaseConnectorExecutor("postgresql"), new NamedDatabaseConnectorExecutor("sqlserver"), new NamedDatabaseConnectorExecutor("sqlite")
        ];
        var registry = new ConnectorRegistry(executors);
        foreach (var type in new[] { "http", "webhook", "slack", "ai", "delay", "email", "smtp", "database", "db", "postgresql", "sqlserver", "sqlite" })
            Assert.Equal(type, registry.Resolve(type).Type);
    }

    private static ConnectorExecutionContext Context(ConnectorRetryPolicy retry) => new("tenant-a", "test", "test.execute", null, new Dictionary<string, string> { ["vertex:connector.requestsPerSecond"] = "1000" }, new Dictionary<string, object>(), retry);

    private sealed class DelayingExecutor : IConnectorExecutor
    {
        public string Type => "test";
        public async Task<ConnectorExecutionResult> ExecuteAsync(ConnectorExecutionContext context, CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException();
        }
    }

    private sealed class SuccessfulHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK));
    }

    private sealed class ScriptedExecutor(params ConnectorExecutionResult[] results) : IConnectorExecutor
    {
        private readonly Queue<ConnectorExecutionResult> _results = new(results);
        public string Type => "test";
        public int Calls { get; private set; }
        public Task<ConnectorExecutionResult> ExecuteAsync(ConnectorExecutionContext context, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(_results.Dequeue());
        }
    }
}
