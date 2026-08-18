using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Exceptions;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.Application.Connectors;

public sealed record ConnectorExecutionContext(
    string TenantId,
    string Type,
    string OperationId,
    Uri? Endpoint,
    IReadOnlyDictionary<string, string> Attributes,
    IDictionary<string, object> Variables,
    ConnectorRetryPolicy RetryPolicy,
    string? CredentialId = null,
    string? CredentialSecret = null);

public sealed record ConnectorExecutionResult(
    bool Success,
    int? StatusCode,
    IReadOnlyDictionary<string, object> Outputs,
    string? ErrorCode = null,
    int Attempts = 1,
    long DurationMilliseconds = 0);

public sealed record ConnectorRetryPolicy(int MaxAttempts = 3, TimeSpan? Timeout = null, TimeSpan? InitialDelay = null)
{
    public TimeSpan EffectiveTimeout => Timeout ?? TimeSpan.FromSeconds(30);
    public TimeSpan EffectiveDelay => InitialDelay ?? TimeSpan.FromMilliseconds(250);
}

public interface IConnectorExecutor
{
    string Type { get; }
    Task<ConnectorExecutionResult> ExecuteAsync(ConnectorExecutionContext context, CancellationToken cancellationToken);
}

public interface IConnectorRegistry { IConnectorExecutor Resolve(string type); }
public interface IConnectorRuntime { Task<ConnectorExecutionResult> ExecuteAsync(ConnectorExecutionContext context, CancellationToken cancellationToken = default); }

public sealed class ConnectorRegistry(IEnumerable<IConnectorExecutor> executors) : IConnectorRegistry
{
    private readonly IReadOnlyDictionary<string, IConnectorExecutor> _executors = executors.ToDictionary(x => x.Type, StringComparer.OrdinalIgnoreCase);
    public IConnectorExecutor Resolve(string type) => _executors.TryGetValue(type, out var executor)
        ? executor
        : throw new ServiceTaskExecutionException($"No connector executor is registered for '{type}'.");
}

public sealed class ConnectorRateLimitPolicy
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _gates = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, long> _lastTicks = new(StringComparer.OrdinalIgnoreCase);

    public async Task<IDisposable> AcquireAsync(string key, int requestsPerSecond, CancellationToken cancellationToken)
    {
        var gate = _gates.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            var minimumDelay = TimeSpan.FromSeconds(1d / Math.Clamp(requestsPerSecond, 1, 1000));
            if (_lastTicks.TryGetValue(key, out var ticks))
            {
                var remaining = minimumDelay - Stopwatch.GetElapsedTime(ticks);
                if (remaining > TimeSpan.Zero) await Task.Delay(remaining, cancellationToken);
            }
            _lastTicks[key] = Stopwatch.GetTimestamp();
            return new Releaser(gate);
        }
        catch
        {
            gate.Release();
            throw;
        }
    }

    private sealed class Releaser(SemaphoreSlim gate) : IDisposable
    {
        public void Dispose() => gate.Release();
    }
}

public sealed class ConnectorRedactionPolicy
{
    private static readonly string[] SensitiveFragments = ["secret", "token", "password", "authorization", "apikey", "api-key", "connectionstring"];
    public bool IsSensitive(string key) => SensitiveFragments.Any(fragment => key.Contains(fragment, StringComparison.OrdinalIgnoreCase));
    public IReadOnlyDictionary<string, object> Redact(IReadOnlyDictionary<string, object> values) =>
        values.ToDictionary(pair => pair.Key, pair => IsSensitive(pair.Key) ? (object)"***" : pair.Value, StringComparer.OrdinalIgnoreCase);
}

public sealed class ConnectorRuntime(
    IConnectorRegistry registry,
    ConnectorRateLimitPolicy rateLimiter,
    ConnectorRedactionPolicy redaction,
    ILogger<ConnectorRuntime> logger) : IConnectorRuntime
{
    public async Task<ConnectorExecutionResult> ExecuteAsync(ConnectorExecutionContext context, CancellationToken cancellationToken = default)
    {
        var executor = registry.Resolve(context.Type);
        var rate = GetInt(context.Attributes, "vertex:connector.requestsPerSecond", 10, 1, 1000);
        var key = $"{context.TenantId}:{context.Type}:{context.Endpoint?.Host ?? "local"}";
        using var lease = await rateLimiter.AcquireAsync(key, rate, cancellationToken);
        var stopwatch = Stopwatch.StartNew();

        for (var attempt = 1; attempt <= context.RetryPolicy.MaxAttempts; attempt++)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(context.RetryPolicy.EffectiveTimeout);
            try
            {
                var raw = await executor.ExecuteAsync(context, timeout.Token);
                var result = raw with { Outputs = redaction.Redact(raw.Outputs), Attempts = attempt, DurationMilliseconds = stopwatch.ElapsedMilliseconds };
                if (result.Success || attempt == context.RetryPolicy.MaxAttempts) return result;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                if (attempt == context.RetryPolicy.MaxAttempts)
                    return new ConnectorExecutionResult(false, null, new Dictionary<string, object>(), "timeout", attempt, stopwatch.ElapsedMilliseconds);
            }
            catch (Exception exception) when (exception is HttpRequestException or System.Data.Common.DbException or System.Net.Mail.SmtpException)
            {
                if (attempt == context.RetryPolicy.MaxAttempts)
                    return new ConnectorExecutionResult(false, null, new Dictionary<string, object>(), MapError(exception), attempt, stopwatch.ElapsedMilliseconds);
            }

            logger.LogWarning("Connector {ConnectorType} attempt {Attempt} failed; retrying", context.Type, attempt);
            await Task.Delay(TimeSpan.FromMilliseconds(context.RetryPolicy.EffectiveDelay.TotalMilliseconds * Math.Pow(2, attempt - 1)), cancellationToken);
        }
        throw new InvalidOperationException("Connector retry loop terminated unexpectedly.");
    }

    private static int GetInt(IReadOnlyDictionary<string, string> values, string key, int fallback, int minimum, int maximum) =>
        values.TryGetValue(key, out var raw) && int.TryParse(raw, out var parsed) ? Math.Clamp(parsed, minimum, maximum) : fallback;
    private static string MapError(Exception exception) => exception switch
    {
        HttpRequestException => "network_error",
        System.Data.Common.DbException => "database_error",
        System.Net.Mail.SmtpException => "smtp_error",
        _ => "connector_error"
    };
}

public sealed class VertexConnectorServiceTaskHandler(
    IConnectorRuntime runtime,
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration) : IServiceTaskHandler
{
    public async Task ExecuteAsync(IDictionary<string, string> attributes, IDictionary<string, object> variables, CancellationToken ct = default)
    {
        var type = Required(attributes, "vertex:connector.type");
        var operationId = Required(attributes, "vertex:connector.operationId");
        var tenantId = Value(attributes, "vertex:connector.tenantId") ?? GetVariable(variables, "tenantId") ?? "default";
        var endpoint = Uri.TryCreate(Value(attributes, "vertex:connector.endpoint"), UriKind.Absolute, out var parsed) ? parsed : null;
        var credentialId = Value(attributes, "vertex:connector.credentialRef") ?? Value(attributes, "vertex:credential.id");
        var secretKey = Value(attributes, "vertex:connector.secretKey") ?? "token";
        var credentialDestinationHost = GetCredentialDestinationHost(type, endpoint, attributes);
        var secret = await ResolveSecretAsync(tenantId, credentialId, secretKey, credentialDestinationHost, ct);
        var retries = GetInt(attributes, "vertex:retryPolicy.maxAttempts", 3, 1, 10);
        var timeout = GetInt(attributes, "vertex:retryPolicy.timeoutMs", GetInt(attributes, "vertex:connector.timeoutMs", 30_000, 100, 300_000), 100, 300_000);
        var delay = GetInt(attributes, "vertex:retryPolicy.initialDelayMs", GetInt(attributes, "vertex:retryPolicy.baseDelayMs", 250, 0, 60_000), 0, 60_000);
        var context = new ConnectorExecutionContext(tenantId, type, operationId, endpoint, new Dictionary<string, string>(attributes), variables, new ConnectorRetryPolicy(retries, TimeSpan.FromMilliseconds(timeout), TimeSpan.FromMilliseconds(delay)), credentialId, secret);
        var result = await runtime.ExecuteAsync(context, ct);

        variables["connector.success"] = result.Success;
        variables["connector.status"] = result.StatusCode ?? 0;
        variables["connector.attempts"] = result.Attempts;
        variables["connector.durationMs"] = result.DurationMilliseconds;
        foreach (var output in result.Outputs) variables[$"connector.output.{output.Key}"] = output.Value;
        await AuditAsync(context, result, ct);

        if (!result.Success)
            throw new ServiceTaskExecutionException($"Connector '{type}' failed with code '{result.ErrorCode ?? "unknown"}'.");
    }

    private async Task<string?> ResolveSecretAsync(string tenantId, string? credentialId, string secretKey, string? destinationHost, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(credentialId)) return null;
        if (destinationHost is not null && !IsAllowedCredentialHost(destinationHost))
            throw new ServiceTaskExecutionException($"Credential transmission to host '{destinationHost}' is not allowed.");
        await using var scope = scopeFactory.CreateAsyncScope();
        var credentialService = scope.ServiceProvider.GetRequiredService<ICredentialService>();
        return await credentialService.ResolveSecretAsync(tenantId, credentialId, secretKey, ct)
            ?? throw new ServiceTaskExecutionException("The configured credential or secret key was not found.");
    }

    private static string? GetCredentialDestinationHost(
        string connectorType,
        Uri? endpoint,
        IDictionary<string, string> attributes)
    {
        if (endpoint is not null)
            return endpoint.Host;

        return connectorType is "email" or "smtp"
            ? Value(attributes, "vertex:connector.smtpHost")
            : null;
    }

    private bool IsAllowedCredentialHost(string host)
    {
        var hosts = configuration.GetSection("ConnectorRuntime:AllowedCredentialHosts").Get<string[]>() ?? [];
        return hosts.Any(allowed => string.Equals(allowed, host, StringComparison.OrdinalIgnoreCase));
    }

    private async Task AuditAsync(ConnectorExecutionContext context, ConnectorExecutionResult result, CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var audit = scope.ServiceProvider.GetService<IAuditLogService>();
        if (audit is null) return;
        await audit.RecordAsync(new AuditLog
        {
            Timestamp = DateTimeOffset.UtcNow,
            Action = "connector.executed",
            Resource = "connector-runtime",
            ResourceId = context.OperationId,
            TenantId = context.TenantId,
            StatusCode = result.Success ? 200 : result.StatusCode ?? 500,
            DetailsJson = JsonSerializer.Serialize(new { context.Type, context.OperationId, EndpointHost = context.Endpoint?.Host, result.Success, result.StatusCode, result.ErrorCode, result.Attempts, result.DurationMilliseconds, CredentialUsed = context.CredentialId is not null })
        }, ct);
    }

    private static string Required(IDictionary<string, string> values, string key) => Value(values, key) ?? throw new ServiceTaskExecutionException($"'{key}' is required.");
    private static string? Value(IDictionary<string, string> values, string key) => values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value.Trim() : null;
    private static string? GetVariable(IDictionary<string, object> values, string key) => values.TryGetValue(key, out var value) ? Convert.ToString(value) : null;
    private static int GetInt(IDictionary<string, string> values, string key, int fallback, int min, int max) => values.TryGetValue(key, out var raw) && int.TryParse(raw, out var parsed) ? Math.Clamp(parsed, min, max) : fallback;
}
