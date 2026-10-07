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

public sealed class ConnectorRuntime(
	IConnectorRegistry registry,
	ConnectorRateLimitPolicy rateLimiter,
	ConnectorRedactionPolicy redaction,
	ILogger<ConnectorRuntime> logger,
	ConnectorDestinationPolicy? destinationPolicy = null) : IConnectorRuntime
{
	public async Task<ConnectorExecutionResult> ExecuteAsync(ConnectorExecutionContext context, CancellationToken cancellationToken = default)
	{
		if (destinationPolicy is not null)
		{
			await destinationPolicy.ValidateAsync(context, cancellationToken);
		}

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
				if (result.Success || attempt == context.RetryPolicy.MaxAttempts)
				{
					return result;
				}
			}
			catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
			{
				if (attempt == context.RetryPolicy.MaxAttempts)
				{
					return new ConnectorExecutionResult(false, null, new Dictionary<string, object>(StringComparer.Ordinal), "timeout", attempt, stopwatch.ElapsedMilliseconds);
				}
			}
			catch (Exception exception) when (exception is HttpRequestException or System.Data.Common.DbException or System.Net.Mail.SmtpException)
			{
				if (attempt == context.RetryPolicy.MaxAttempts)
				{
					return new ConnectorExecutionResult(false, null, new Dictionary<string, object>(StringComparer.Ordinal), MapError(exception), attempt, stopwatch.ElapsedMilliseconds);
				}
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
