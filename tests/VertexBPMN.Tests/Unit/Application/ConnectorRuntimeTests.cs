using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Text;
using VertexBPMN.Application.Connectors;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Exceptions;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.Tests.Unit.Application;

public sealed class ConnectorRuntimeTests
{
	[Fact]
	public async Task Runtime_RetriesAndRedactsSensitiveOutputs()
	{
		var executor = new ScriptedExecutor(
			new ConnectorExecutionResult(false, 503, new Dictionary<string, object>(StringComparer.Ordinal), "remote_server_error"),
			new ConnectorExecutionResult(true, 200, new Dictionary<string, object>(StringComparer.Ordinal) { ["token"] = "clear-secret", ["status"] = "ok" }));
		var runtime = new ConnectorRuntime(new ConnectorRegistry([executor]), new ConnectorRateLimitPolicy(), new ConnectorRedactionPolicy(), NullLogger<ConnectorRuntime>.Instance);
		var result = await runtime.ExecuteAsync(Context(new ConnectorRetryPolicy(2, TimeSpan.FromSeconds(1), TimeSpan.Zero)), TestContext.Current.CancellationToken);

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
		runtime.Setup(x => x.ExecuteAsync(It.IsAny<ConnectorExecutionContext>(), It.IsAny<CancellationToken>())).Callback<ConnectorExecutionContext, CancellationToken>((context, _) => capturedContext = context).ReturnsAsync(new ConnectorExecutionResult(true, 200, new Dictionary<string, object>(StringComparer.Ordinal)));
		await using var provider = new ServiceCollection().AddSingleton(credentials.Object).AddSingleton(audit.Object).BuildServiceProvider();
		var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal) { ["ConnectorRuntime:AllowedCredentialHosts:0"] = "api.example.test" }).Build();
		var handler = new VertexConnectorServiceTaskHandler(runtime.Object, provider.GetRequiredService<IServiceScopeFactory>(), configuration);
		var attributes = new Dictionary<string, string>(StringComparer.Ordinal)
		{
			["vertex:connector.type"] = "http",
			["vertex:connector.operationId"] = "http.request",
			["vertex:connector.endpoint"] = "https://api.example.test/orders",
			["vertex:connector.tenantId"] = "tenant-a",
			["vertex:connector.credentialRef"] = "credential-1"
		};

		await handler.ExecuteAsync(attributes, new Dictionary<string, object>(StringComparer.Ordinal), TestContext.Current.CancellationToken);

		Assert.Equal(secret, capturedContext!.CredentialSecret);
		Assert.NotNull(capturedAudit);
		Assert.DoesNotContain(secret, capturedAudit.DetailsJson ?? string.Empty, StringComparison.Ordinal);
		Assert.Contains("connector.executed", capturedAudit.Action, StringComparison.Ordinal);
	}

	[Fact]
	public async Task Handler_RejectsCredentialForHostOutsideAllowlistBeforeResolution()
	{
		var credentials = new Mock<ICredentialService>();
		await using var provider = new ServiceCollection().AddSingleton(credentials.Object).BuildServiceProvider();
		var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal) { ["ConnectorRuntime:AllowedCredentialHosts:0"] = "allowed.example.test" }).Build();
		var handler = new VertexConnectorServiceTaskHandler(Mock.Of<IConnectorRuntime>(), provider.GetRequiredService<IServiceScopeFactory>(), configuration);
		var attributes = new Dictionary<string, string>(StringComparer.Ordinal)
		{
			["vertex:connector.type"] = "http",
			["vertex:connector.operationId"] = "http.request",
			["vertex:connector.endpoint"] = "https://blocked.example.test/orders",
			["vertex:connector.credentialRef"] = "credential-1"
		};

		await Assert.ThrowsAsync<VertexBPMN.Domain.Exceptions.ServiceTaskExecutionException>(() => handler.ExecuteAsync(attributes, new Dictionary<string, object>(StringComparer.Ordinal), TestContext.Current.CancellationToken));
		credentials.Verify(x => x.ResolveSecretAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
	}

	[Fact]
	public async Task Handler_RejectsSmtpCredentialForHostOutsideAllowlistBeforeResolution()
	{
		var credentials = new Mock<ICredentialService>();
		await using var provider = new ServiceCollection().AddSingleton(credentials.Object).BuildServiceProvider();
		var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal)
		{
			["ConnectorRuntime:AllowedCredentialHosts:0"] = "mail.example.test"
		}).Build();
		var handler = new VertexConnectorServiceTaskHandler(
			Mock.Of<IConnectorRuntime>(),
			provider.GetRequiredService<IServiceScopeFactory>(),
			configuration);
		var attributes = new Dictionary<string, string>(StringComparer.Ordinal)
		{
			["vertex:connector.type"] = "smtp",
			["vertex:connector.operationId"] = "smtp.send",
			["vertex:connector.smtpHost"] = "blocked.example.test",
			["vertex:connector.credentialRef"] = "credential-1"
		};

		await Assert.ThrowsAsync<VertexBPMN.Domain.Exceptions.ServiceTaskExecutionException>(
			() => handler.ExecuteAsync(attributes, new Dictionary<string, object>(StringComparer.Ordinal), TestContext.Current.CancellationToken));
		credentials.Verify(
			x => x.ResolveSecretAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
			Times.Never);
	}

	[Fact]
	public async Task Runtime_MapsTimeoutWithoutLeakingExceptionDetails()
	{
		var executor = new DelayingExecutor();
		var runtime = new ConnectorRuntime(new ConnectorRegistry([executor]), new ConnectorRateLimitPolicy(), new ConnectorRedactionPolicy(), NullLogger<ConnectorRuntime>.Instance);
		var result = await runtime.ExecuteAsync(Context(new ConnectorRetryPolicy(1, TimeSpan.FromMilliseconds(20), TimeSpan.Zero)), TestContext.Current.CancellationToken);
		Assert.False(result.Success);
		Assert.Equal("timeout", result.ErrorCode);
		Assert.Empty(result.Outputs);
	}

	[Fact]
	public async Task RateLimit_SpacesExecutionsForSameTenantTypeAndHost()
	{
		var executor = new ScriptedExecutor(
			new ConnectorExecutionResult(true, 200, new Dictionary<string, object>(StringComparer.Ordinal)),
			new ConnectorExecutionResult(true, 200, new Dictionary<string, object>(StringComparer.Ordinal)));
		var limiter = new ConnectorRateLimitPolicy();
		var runtime = new ConnectorRuntime(new ConnectorRegistry([executor]), limiter, new ConnectorRedactionPolicy(), NullLogger<ConnectorRuntime>.Instance);
		var context = new ConnectorExecutionContext("tenant-a", "test", "test.execute", new Uri("https://api.example.test"), new Dictionary<string, string>(StringComparer.Ordinal) { ["vertex:connector.requestsPerSecond"] = "10" }, new Dictionary<string, object>(StringComparer.Ordinal), new ConnectorRetryPolicy(1));
		await runtime.ExecuteAsync(context, TestContext.Current.CancellationToken);
		var started = System.Diagnostics.Stopwatch.StartNew();
		await runtime.ExecuteAsync(context, TestContext.Current.CancellationToken);
		Assert.True(started.Elapsed >= TimeSpan.FromMilliseconds(75));
	}

	[Fact]
	public void Registry_ResolvesEveryBuiltInConnectorType()
	{
		using var clientHandler = new SuccessfulHandler();
		using var client = new HttpClient(clientHandler, disposeHandler: false);
		IConnectorExecutor[] executors =
		[
			new HttpConnectorExecutor(client), new NamedHttpConnectorExecutor(client, "webhook"), new NamedHttpConnectorExecutor(client, "slack"), new NamedHttpConnectorExecutor(client, "ai"),
			new DelayConnectorExecutor(), new EmailConnectorExecutor(), new NamedEmailConnectorExecutor("smtp"), new DatabaseConnectorExecutor(),
			new NamedDatabaseConnectorExecutor("db"), new NamedDatabaseConnectorExecutor("postgresql"), new NamedDatabaseConnectorExecutor("sqlserver"), new NamedDatabaseConnectorExecutor("sqlite")
		];
		var registry = new ConnectorRegistry(executors);
		foreach (var type in new[] { "http", "webhook", "slack", "ai", "delay", "email", "smtp", "database", "db", "postgresql", "sqlserver", "sqlite" })
		{
			Assert.Equal(type, registry.Resolve(type).Type);
		}
	}

	private static ConnectorExecutionContext Context(ConnectorRetryPolicy retry) => new("tenant-a", "test", "test.execute", null, new Dictionary<string, string>(StringComparer.Ordinal) { ["vertex:connector.requestsPerSecond"] = "1000" }, new Dictionary<string, object>(StringComparer.Ordinal), retry);

	[Fact]
	public async Task HttpExecutor_ResolvesPathPlaceholdersFromVariables()
	{
		Uri? captured = null;
		using var httpClientHandler = new RecordingHandler(r => captured = r.RequestUri);
		using var httpClient = new HttpClient(httpClientHandler, disposeHandler: false);
		var executor = new HttpConnectorExecutor(httpClient);
		var context = new ConnectorExecutionContext(
			"tenant-a", "http", "get.user", new Uri("http://example.test/users/{id}"),
			new Dictionary<string, string>(StringComparer.Ordinal) { ["vertex:connector.method"] = "GET" },
			new Dictionary<string, object>(StringComparer.Ordinal) { ["id"] = 42 },
			new ConnectorRetryPolicy(0, TimeSpan.Zero, TimeSpan.Zero));
		await executor.ExecuteAsync(context, TestContext.Current.CancellationToken);
		Assert.Equal("http://example.test/users/42", captured!.ToString());
	}

	[Fact]
	public async Task HttpExecutor_TransportsBindingHeadersOnRequest()
	{
		HttpRequestMessage? captured = null;
		using var httpClientHandler = new RecordingHandler(r => captured = r);
		using var httpClient = new HttpClient(httpClientHandler, disposeHandler: false);
		var executor = new HttpConnectorExecutor(httpClient);
		var context = new ConnectorExecutionContext(
			"tenant-a", "http", "get.user", new Uri("https://api.example.test/v1/users"),
			new Dictionary<string, string>(StringComparer.Ordinal)
			{
				["vertex:connector.method"] = "POST",
				["vertex:connector.contentType"] = "application/json",
				["vertex:connector.body"] = "{\"a\":1}",
				["vertex:connector.header.X-Correlation-Id"] = "corr-123",
				["vertex:connector.header.Accept"] = "application/json"
			},
			new Dictionary<string, object>(StringComparer.Ordinal),
			new ConnectorRetryPolicy(0, TimeSpan.Zero, TimeSpan.Zero));
		await executor.ExecuteAsync(context, TestContext.Current.CancellationToken);

		Assert.Equal(HttpMethod.Post, captured!.Method);
		Assert.Equal("corr-123", captured.Headers.GetValues("X-Correlation-Id").Single());
		Assert.Equal("application/json", captured.Headers.GetValues("Accept").Single());
	}

	[Fact]
	public async Task HttpExecutor_HonoursBindingTimeoutOverGlobalRetryTimeout()
	{
		using var httpClientHandler = new DelayingSendHandler(TimeSpan.FromMilliseconds(600));
		using var httpClient = new HttpClient(httpClientHandler, disposeHandler: false);
		var executor = new HttpConnectorExecutor(httpClient);
		var context = new ConnectorExecutionContext(
			"tenant-a", "http", "slow", new Uri("https://api.example.test/slow"),
			new Dictionary<string, string>(StringComparer.Ordinal)
			{
				["vertex:connector.method"] = "GET",
				["vertex:connector.timeoutMs"] = "50"
			},
			new Dictionary<string, object>(StringComparer.Ordinal),
			new ConnectorRetryPolicy(0, TimeSpan.FromSeconds(5), TimeSpan.Zero));
		await Assert.ThrowsAnyAsync<OperationCanceledException>(
			() => executor.ExecuteAsync(context, TestContext.Current.CancellationToken));
	}

	[Fact]
	public void BindingVersion_SupportsLegacyAndCurrentAndRejectsUnknown()
	{
		Assert.True(ConnectorBindingVersion.IsSupported(ConnectorBindingVersion.Legacy));
		Assert.True(ConnectorBindingVersion.IsSupported(ConnectorBindingVersion.Versioned));
		Assert.False(ConnectorBindingVersion.IsSupported(3));
		Assert.False(ConnectorBindingVersion.IsSupported(0));
	}

	[Fact]
	public void ValueResolver_ResolvesLiteralVariableAndMixedSources()
	{
		var vars = new Dictionary<string, object?>(StringComparer.Ordinal)
		{
			["userId"] = "42",
			["kind"] = "premium"
		};
		Assert.Equal("http://x.test", ConnectorValueResolver.Resolve("http://x.test", vars));
		Assert.Equal("42", ConnectorValueResolver.Resolve("{{userId}}", vars));
		Assert.Equal("42/premium", ConnectorValueResolver.Resolve("{{userId}}/{{kind}}", vars));
		Assert.Equal("id=42", ConnectorValueResolver.Resolve("id={{userId}}", vars));
	}

	[Fact]
	public void ValueResolver_ThrowsOnUnresolvedVariable()
	{
		var vars = new Dictionary<string, object?>(StringComparer.Ordinal);
		Assert.Throws<ServiceTaskExecutionException>(() => ConnectorValueResolver.Resolve("{{missing}}", vars));
		Assert.Throws<ServiceTaskExecutionException>(() => ConnectorValueResolver.Resolve("a {{broken", vars));
	}

	[Fact]
	public async Task Handler_BridgesStudioIoMappingFormToPlainConnectorKeys()
	{
		// Studio persists connector config as ioMapping inputs; the handler must
		// surface vertex:ioMapping.input.vertex:connector.* under plain vertex:connector.*
		ConnectorExecutionContext? capturedContext = null;
		var runtime = new Mock<IConnectorRuntime>();
		runtime.Setup(x => x.ExecuteAsync(It.IsAny<ConnectorExecutionContext>(), It.IsAny<CancellationToken>())).Callback<ConnectorExecutionContext, CancellationToken>((context, _) => capturedContext = context).ReturnsAsync(new ConnectorExecutionResult(true, 200, new Dictionary<string, object>(StringComparer.Ordinal)));
		await using var provider = new ServiceCollection().AddSingleton<ICredentialService>(new Mock<ICredentialService>().Object).AddSingleton<IAuditLogService>(new Mock<IAuditLogService>().Object).BuildServiceProvider();
		var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal)).Build();
		var handler = new VertexConnectorServiceTaskHandler(runtime.Object, provider.GetRequiredService<IServiceScopeFactory>(), configuration);
		var attributes = new Dictionary<string, string>(StringComparer.Ordinal)
		{
			["vertex:connector.type"] = "http",
			["vertex:connector.operationId"] = "http.request",
			["vertex:ioMapping.input.vertex:connector.endpoint"] = "https://api.example.test/orders",
			["vertex:ioMapping.input.vertex:connector.method"] = "GET",
			["vertex:ioMapping.input.vertex:connector.header.X-Api-Key"] = "abc123",
			["vertex:ioMapping.input.vertex:connector.timeoutMs"] = "15000"
		};

		await handler.ExecuteAsync(attributes, new Dictionary<string, object>(StringComparer.Ordinal), TestContext.Current.CancellationToken);

		Assert.NotNull(capturedContext);
		Assert.Equal("https://api.example.test/orders", capturedContext.Attributes["vertex:connector.endpoint"]);
		Assert.Equal("GET", capturedContext.Attributes["vertex:connector.method"]);
		Assert.Equal("abc123", capturedContext.Attributes["vertex:connector.header.X-Api-Key"]);
		Assert.Equal("15000", capturedContext.Attributes["vertex:connector.timeoutMs"]);
	}

	[Fact]
	public async Task HttpExecutor_RejectsBlockedAndCtlBindingHeaders()
	{
		using var httpClientHandler = new RecordingHandler(_ => { });
		using var httpClient = new HttpClient(httpClientHandler, disposeHandler: false);
		var executor = new HttpConnectorExecutor(httpClient);
		async Task Run(IDictionary<string, string> attributes)
		{
			var context = new ConnectorExecutionContext(
				"tenant-a", "http", "x", new Uri("https://api.example.test/v1"),
				new Dictionary<string, string>(attributes, StringComparer.Ordinal),
				new Dictionary<string, object>(StringComparer.Ordinal),
				new ConnectorRetryPolicy(0, TimeSpan.Zero, TimeSpan.Zero));
			await executor.ExecuteAsync(context, TestContext.Current.CancellationToken);
		}

		await Assert.ThrowsAsync<ServiceTaskExecutionException>(() => Run(new Dictionary<string, string>(StringComparer.Ordinal) { ["vertex:connector.header.Authorization"] = "Bearer abc" }));
		await Assert.ThrowsAsync<ServiceTaskExecutionException>(() => Run(new Dictionary<string, string>(StringComparer.Ordinal) { ["vertex:connector.header.Host"] = "other.example.test" }));
		await Assert.ThrowsAsync<ServiceTaskExecutionException>(() => Run(new Dictionary<string, string>(StringComparer.Ordinal) { ["vertex:connector.header.X-Custom"] = "a\r\nInjected: yes" }));
	}

	[Fact]
	public async Task HttpExecutor_NeverExportsSetCookieHeader()
	{
		using var httpClientHandler = new SetCookieHandler();
		using var httpClient = new HttpClient(httpClientHandler, disposeHandler: false);
		var executor = new HttpConnectorExecutor(httpClient);
		var context = new ConnectorExecutionContext(
			"tenant-a", "http", "x", new Uri("https://api.example.test/v1"),
			new Dictionary<string, string>(StringComparer.Ordinal) { ["vertex:connector.method"] = "GET", ["vertex:connector.outputHeader.Set-Cookie"] = "Set-Cookie" },
			new Dictionary<string, object>(StringComparer.Ordinal),
			new ConnectorRetryPolicy(0, TimeSpan.Zero, TimeSpan.Zero));
		var result = await executor.ExecuteAsync(context, TestContext.Current.CancellationToken);
		Assert.True(result.Success);
		Assert.False(result.Outputs.ContainsKey("Set-Cookie"));
	}

	[Fact]
	public async Task Handler_RejectsUnsupportedBindingVersion()
	{
		var handler = new VertexConnectorServiceTaskHandler(Mock.Of<IConnectorRuntime>(), Mock.Of<IServiceScopeFactory>(), Mock.Of<IConfiguration>());
		var attributes = new Dictionary<string, string>(StringComparer.Ordinal)
		{
			["vertex:connector.type"] = "http",
			["vertex:connector.operationId"] = "x",
			["vertex:connector.bindingVersion"] = "3"
		};
		await Assert.ThrowsAsync<ServiceTaskExecutionException>(() => handler.ExecuteAsync(attributes, new Dictionary<string, object>(StringComparer.Ordinal), TestContext.Current.CancellationToken));
	}

	private sealed class SetCookieHandler : HttpMessageHandler
	{
		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK);
			response.Headers.Add("Set-Cookie", "sid=abc123; HttpOnly");
			return Task.FromResult(response);
		}
	}

	[Fact]
	public async Task HttpExecutor_ExposesJsonResponseFieldsWithRedaction()
	{
		using var httpClientHandler = new JsonSendHandler("""{"customer":"A","amount":12.5,"secretToken":"s"}""");
		using var httpClient = new HttpClient(httpClientHandler, disposeHandler: false);
		var reader = new HttpDataResponseReader(4096, new ConnectorRedactionPolicy());
		var executor = new HttpConnectorExecutor(httpClient, reader);
		var context = new ConnectorExecutionContext(
			"tenant-a", "http", "get.orders", new Uri("https://api.example.test/v1/orders"),
			new Dictionary<string, string>(StringComparer.Ordinal) { ["vertex:connector.method"] = "GET" },
			new Dictionary<string, object>(StringComparer.Ordinal),
			new ConnectorRetryPolicy(0, TimeSpan.Zero, TimeSpan.Zero));

		var result = await executor.ExecuteAsync(context, TestContext.Current.CancellationToken);

		Assert.True(result.Success);
		Assert.Equal("A", result.Outputs["httpBody.customer"]);
		Assert.Equal(12.5, result.Outputs["httpBody.amount"]);
		Assert.Equal("***", result.Outputs["httpBody.secretToken"]);
		Assert.Equal("application/json", result.Outputs["httpContentType"]);
	}

	private sealed class JsonSendHandler(string json) : HttpMessageHandler
	{
		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
			{
				Content = new StringContent(json, Encoding.UTF8, "application/json")
			};
			return Task.FromResult(response);
		}
	}

	private sealed class RecordingHandler(Action<HttpRequestMessage> onRequest) : HttpMessageHandler
	{
		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			onRequest(request);
			return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK));
		}
	}
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

	private sealed class DelayingSendHandler(TimeSpan delay) : HttpMessageHandler
	{
		protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			await Task.Delay(delay, cancellationToken);
			return new HttpResponseMessage(System.Net.HttpStatusCode.OK);
		}
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
