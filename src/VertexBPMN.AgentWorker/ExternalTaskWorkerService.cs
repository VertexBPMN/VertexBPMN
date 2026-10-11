using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.AgentWorker;
internal sealed class ExternalTaskWorkerService(IExternalTaskApiClient api, IEnumerable<IExternalTaskHandler> handlers, IOptions<ExternalTaskWorkerOptions> options, ILogger<ExternalTaskWorkerService> logger, TimeProvider? clock = null) : BackgroundService
{
	private readonly ConcurrentDictionary<Guid, Task> _active = new();
	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		var settings = options.Value;
		if (!settings.Enabled)
		{
			return;
		}

		var byTopic = GetTopicHandlers(settings.Topics);
		var topics = settings.Topics;

		var failures = 0;
		try
		{
			while (!stoppingToken.IsCancellationRequested)
			{
				await ObserveCompletedTasksAsync(stoppingToken);

				var capacity = settings.MaxConcurrency - _active.Count;
				if (capacity <= 0)
				{
					await Task.Delay(settings.PollMilliseconds, stoppingToken);
					continue;
				}

				try
				{
					var claimed = await api.ClaimAsync(topics, Math.Min(capacity, settings.MaxTasksPerClaim), settings.LeaseSeconds, stoppingToken);
					failures = 0;
					foreach (var lease in claimed)
					{
						if (!byTopic.TryGetValue(lease.Topic, out var handler))
						{
							continue;
						}

						_active.TryAdd(lease.JobId, RunLeaseAsync(lease, handler, settings, stoppingToken));
					}

					if (claimed.Count == 0)
					{
						await Task.Delay(settings.PollMilliseconds, stoppingToken);
					}
				}
				catch (ExternalTaskTransportException exception)
				{
					failures++;
					var delay = exception.RetryAfter ?? Backoff(failures, settings.MaximumBackoffSeconds);
					logger.LogWarning("External task API unavailable; polling resumes after {Delay}.", delay);
					await Task.Delay(delay, stoppingToken);
				}
			}
		}
		catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
		{
			logger.LogDebug("External task polling stopped during shutdown.");
		}
		finally
		{
			await DrainTasksAsync(settings.LeaseSeconds, stoppingToken);
		}
	}

	private Dictionary<string, IExternalTaskHandler> GetTopicHandlers(string[] topics)
	{
		var byTopic = handlers.GroupBy(handler => handler.Topic, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.Single(), StringComparer.Ordinal);
		if (topics.Any(topic => !byTopic.ContainsKey(topic)))
		{
			throw new InvalidOperationException("Every configured external task topic requires exactly one handler.");
		}

		return byTopic;
	}

	private async Task ObserveCompletedTasksAsync(CancellationToken stoppingToken)
	{
		foreach (var completed in _active.Where(item => item.Value.IsCompleted).ToArray())
		{
			_active.TryRemove(completed.Key, out _);
			try
			{
				await completed.Value;
			}
			catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
			{
				logger.LogDebug("External task execution cancelled during shutdown.");
			}
			catch (Exception exception)
			{
				logger.LogError(exception, "External task execution ended unexpectedly.");
			}
		}
	}

	private async Task DrainTasksAsync(int leaseSeconds, CancellationToken stoppingToken)
	{
		try
		{
			await Task.WhenAll(_active.Values).WaitAsync(TimeSpan.FromSeconds(leaseSeconds), CancellationToken.None);
		}
		catch (TimeoutException)
		{
			logger.LogWarning("External task handlers exceeded the graceful-shutdown window.");
		}
		catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
		{
			logger.LogDebug("External task handlers cancelled during shutdown.");
		}
	}

	private async Task RunLeaseAsync(ExternalTaskLease lease, IExternalTaskHandler handler, ExternalTaskWorkerOptions settings, CancellationToken shutdown)
	{
		using var execution = CancellationTokenSource.CreateLinkedTokenSource(shutdown);
		var leaseState = new LeaseState(lease.LeaseExpiresAt);
		var work = ExecuteHandlerAsync(lease, handler, execution.Token);
		var heartbeat = KeepLeaseAsync(lease, settings, leaseState, execution.Token);
		try
		{
			await Task.WhenAny(work, heartbeat);
		}
		finally
		{
			try
			{
				await execution.CancelAsync();
			}
			finally
			{
				// Both operations must finish before their shared token source is disposed.
				await Task.WhenAll(work, heartbeat);
			}
		}

		var result = await work;
		var leaseValid = await heartbeat;
		if (result is null || !leaseValid || shutdown.IsCancellationRequested)
		{
			return;
		}

		await SubmitResultAsync(lease, result, leaseState, settings, shutdown);
	}

	private async Task<ExternalTaskHandlerResult?> ExecuteHandlerAsync(ExternalTaskLease lease, IExternalTaskHandler handler, CancellationToken cancellationToken)
	{
		try
		{
			return await handler.HandleAsync(lease, cancellationToken);
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			if (logger.IsEnabled(LogLevel.Debug))
			{
				logger.LogDebug("External task handler cancelled for job {JobId}.", lease.JobId);
			}
			return null;
		}
		catch (Exception)
		{
			logger.LogWarning("External task handler failed; reporting a redacted technical failure for job {JobId}.", lease.JobId);
			return ExternalTaskHandlerResult.TechnicalFailure("transport_failure");
		}
	}

	private async Task<bool> KeepLeaseAsync(ExternalTaskLease lease, ExternalTaskWorkerOptions settings, LeaseState leaseState, CancellationToken cancellationToken)
	{
		var failures = 0;
		var leaseExpiresAt = lease.LeaseExpiresAt;
		while (!cancellationToken.IsCancellationRequested)
		{
			try
			{
				await Task.Delay(TimeSpan.FromSeconds(settings.HeartbeatSeconds), cancellationToken);
			}
			catch (OperationCanceledException)
			{
				return true;
			}

			try
			{
				ExternalTaskWorkerHeartbeatResult heartbeat;
				try
				{
					heartbeat = await api.HeartbeatAsync(lease, settings.LeaseSeconds, cancellationToken);
				}
				catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
				{
					return true;
				}

				if (heartbeat.Outcome == ExternalTaskHeartbeatOutcome.LeaseLost || heartbeat.LeaseExpiresAt is null || heartbeat.LeaseExpiresAt <= leaseExpiresAt)
				{
					return false;
				}

				leaseExpiresAt = heartbeat.LeaseExpiresAt.Value;
				leaseState.ExpiresAt = leaseExpiresAt;
				failures = 0;
			}
			catch (ExternalTaskTransportException exception)
			{
				failures++;
				var now = (clock ?? TimeProvider.System).GetUtcNow().ToUnixTimeMilliseconds();
				var delay = exception.RetryAfter ?? Backoff(failures, Math.Min(settings.HeartbeatSeconds, 5));
				if (now + delay.TotalMilliseconds >= leaseExpiresAt)
				{
					return false;
				}

				try
				{
					await Task.Delay(delay, cancellationToken);
				}
				catch (OperationCanceledException)
				{
					return true;
				}
			}
		}

		return true;
	}

	private async Task SubmitResultAsync(ExternalTaskLease lease, ExternalTaskHandlerResult result, LeaseState leaseState, ExternalTaskWorkerOptions settings, CancellationToken shutdown)
	{
		if (result.Outcome == ExternalTaskHandlerOutcome.Success && result.Result.ValueKind == JsonValueKind.Undefined)
		{
			throw new InvalidOperationException("A successful external task result requires JSON output.");
		}

		if (result.Outcome != ExternalTaskHandlerOutcome.Success && string.IsNullOrWhiteSpace(result.Code))
		{
			throw new InvalidOperationException("A failed external task result requires a stable error code.");
		}

		var receiptId = Guid.NewGuid();
		var failures = 0;
		while (!shutdown.IsCancellationRequested)
		{
			try
			{
				var mutation = result.Outcome switch
				{
					ExternalTaskHandlerOutcome.Success => await api.CompleteAsync(lease, receiptId, result.Result, shutdown),
					ExternalTaskHandlerOutcome.BusinessError => await api.FailAsync(lease, receiptId, "business", result.Code!, shutdown),
					_ => await api.FailAsync(lease, receiptId, "technical", result.Code!, shutdown)};
				if (mutation.Outcome == ExternalTaskMutationOutcome.Rejected)
				{
					logger.LogError("External task result was rejected by the server for job {JobId}.", lease.JobId);
				}

				return;
			}
			catch (ExternalTaskTransportException exception)
			{
				failures++;
				var delay = exception.RetryAfter ?? Backoff(failures, Math.Min(settings.MaximumBackoffSeconds, 5));
				var now = (clock ?? TimeProvider.System).GetUtcNow().ToUnixTimeMilliseconds();
				if (now + delay.TotalMilliseconds >= leaseState.ExpiresAt)
				{
					return;
				}

				await Task.Delay(delay, shutdown);
			}
		}
	}

	private sealed class LeaseState(long expiresAt)
	{
		public long ExpiresAt { get; set; } = expiresAt;
	}

	private static TimeSpan Backoff(int failures, int maximumSeconds)
	{
		var exponent = Math.Min(6, Math.Max(0, failures - 1));
		var seconds = Math.Min(maximumSeconds, 1 << exponent);
		return TimeSpan.FromMilliseconds((seconds * 1000) + RandomNumberGenerator.GetInt32(0, 251));
	}
}
