using Microsoft.AspNetCore.Mvc.Filters;
using System.Collections.Concurrent;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.Api.Services;

/// <summary>
/// Production-Grade Rate Limiting Service
/// Olympic-level feature: Production-Grade Features - Rate Limiting
/// </summary>
internal sealed class ProductionRateLimitingService : BackgroundService, IRateLimitingService
{
	private readonly ConcurrentDictionary<string, RateLimitBucket> _buckets = new(StringComparer.Ordinal);
	private readonly ILogger<ProductionRateLimitingService> _logger;
	private readonly Dictionary<string, RateLimitPolicy> _policies;
	public ProductionRateLimitingService(ILogger<ProductionRateLimitingService> logger)
	{
		_logger = logger;
		_policies = InitializePolicies();
	}

	private static Dictionary<string, RateLimitPolicy> InitializePolicies()
	{
		return new Dictionary<string, RateLimitPolicy>(StringComparer.Ordinal)
		{
			["default"] = new RateLimitPolicy(100, TimeSpan.FromMinutes(1)), // 100 requests per minute
			["authentication"] = new RateLimitPolicy(10, TimeSpan.FromMinutes(5)), // 10 auth attempts per 5 minutes
			["api_creation"] = new RateLimitPolicy(20, TimeSpan.FromMinutes(1)), // 20 creations per minute
			["heavy_operations"] = new RateLimitPolicy(5, TimeSpan.FromMinutes(1)), // 5 heavy ops per minute
			["process_execution"] = new RateLimitPolicy(50, TimeSpan.FromMinutes(1)), // 50 executions per minute
			["monitoring"] = new RateLimitPolicy(200, TimeSpan.FromMinutes(1)), // 200 monitoring calls per minute
			["premium"] = new RateLimitPolicy(500, TimeSpan.FromMinutes(1)), // Premium users: 500 per minute
			["admin"] = new RateLimitPolicy(1000, TimeSpan.FromMinutes(1)) // Admin users: 1000 per minute
		};
	}

	public bool IsAllowed(string identifier, string rateLimitPolicy)
	{
		if (!_policies.TryGetValue(rateLimitPolicy, out var policy))
		{
			_logger.LogWarning("Unknown rate limit policy: {Policy}", rateLimitPolicy);
			policy = _policies["default"];
		}

		var bucketKey = $"{identifier}:{rateLimitPolicy}";
		var bucket = _buckets.GetOrAdd(bucketKey, _ => new RateLimitBucket(policy));
		var allowed = bucket.TryConsume();
		if (!allowed)
		{
			_logger.LogWarning("Rate limit exceeded for {Identifier} with policy {Policy}", identifier, rateLimitPolicy);
		}

		return allowed;
	}

	public RateLimitInfo GetRateLimitInfo(string identifier, string rateLimitPolicy)
	{
		if (!_policies.TryGetValue(rateLimitPolicy, out var policy))
		{
			policy = _policies["default"];
		}

		var bucketKey = $"{identifier}:{rateLimitPolicy}";
		var bucket = _buckets.GetOrAdd(bucketKey, _ => new RateLimitBucket(policy));
		return new RateLimitInfo
		{
			Identifier = identifier,
			Policy = rateLimitPolicy,
			Limit = policy.RequestLimit,
			Remaining = bucket.RemainingRequests,
			ResetTime = bucket.ResetTime,
			WindowDuration = policy.TimeWindow
		};
	}

	public void ResetRateLimit(string identifier, string rateLimitPolicy)
	{
		var bucketKey = $"{identifier}:{rateLimitPolicy}";
		if (_buckets.TryRemove(bucketKey, out _) && _logger.IsEnabled(Microsoft.Extensions.Logging.LogLevel.Information))
		{
			_logger.LogInformation("Rate limit reset for {Identifier} with policy {Policy}", identifier, rateLimitPolicy);
		}
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		while (!stoppingToken.IsCancellationRequested)
		{
			try
			{
				var expiredKeys = _buckets.Where(kvp => kvp.Value.IsExpired).Select(kvp => kvp.Key).ToList();
				foreach (var key in expiredKeys)
				{
					_buckets.TryRemove(key, out _);
				}

				if (expiredKeys.Count > 0 && _logger.IsEnabled(Microsoft.Extensions.Logging.LogLevel.Debug))
				{
					_logger.LogDebug("Cleaned up {Count} expired rate limit buckets", expiredKeys.Count);
				}

				await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
			}
			catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
			{
				return;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error during rate limit bucket cleanup");
				await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
			}
		}
	}
}
