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
				if (remaining > TimeSpan.Zero)
				{
					await Task.Delay(remaining, cancellationToken);
				}
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
