using System.Collections.Concurrent;
using System.Data.Common;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Exceptions;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.Application.Connectors;

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
		var context = new ConnectorExecutionContext(tenantId, type, operationId, endpoint, new Dictionary<string, string>(attributes, StringComparer.Ordinal), variables, new ConnectorRetryPolicy(retries, TimeSpan.FromMilliseconds(timeout), TimeSpan.FromMilliseconds(delay)), credentialId, secret);
		var result = await runtime.ExecuteAsync(context, ct);

		variables["connector.success"] = result.Success;
		variables["connector.status"] = result.StatusCode ?? 0;
		variables["connector.attempts"] = result.Attempts;
		variables["connector.durationMs"] = result.DurationMilliseconds;
		foreach (var output in result.Outputs)
		{
			variables[$"connector.output.{output.Key}"] = output.Value;
		}

		await AuditAsync(context, result, ct);

		if (!result.Success)
		{
			throw new ServiceTaskExecutionException($"Connector '{type}' failed with code '{result.ErrorCode ?? "unknown"}'.");
		}
	}

	private async Task<string?> ResolveSecretAsync(string tenantId, string? credentialId, string secretKey, string? destinationHost, CancellationToken ct)
	{
		if (string.IsNullOrWhiteSpace(credentialId))
		{
			return null;
		}

		if (destinationHost is not null && !IsAllowedCredentialHost(destinationHost))
		{
			throw new ServiceTaskExecutionException($"Credential transmission to host '{destinationHost}' is not allowed.");
		}

		await using var scope = scopeFactory.CreateAsyncScope();
		var credentialService = scope.ServiceProvider.GetRequiredService<ICredentialService>();
		var credential = await credentialService.GetAsync(tenantId, credentialId, ct);
		if (credential?.Type.Equals("oauth2", StringComparison.OrdinalIgnoreCase) == true)
		{
			var flowService = scope.ServiceProvider.GetRequiredService<IOAuth2CredentialFlowService>();
			return await flowService.ResolveValidAccessTokenAsync(tenantId, credentialId, ct)
				?? throw new ServiceTaskExecutionException("The OAuth2 credential has no valid access token; re-authorization is required.");
		}
		return await credentialService.ResolveSecretAsync(tenantId, credentialId, secretKey, ct)
			?? throw new ServiceTaskExecutionException("The configured credential or secret key was not found.");
	}

	private static string? GetCredentialDestinationHost(
		string connectorType,
		Uri? endpoint,
		IDictionary<string, string> attributes)
	{
		if (endpoint is not null)
		{
			return endpoint.Host;
		}

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
		if (audit is null)
		{
			return;
		}

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
