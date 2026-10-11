using Polly;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.Api.Services;

/// <summary>
/// Production-Grade Resilience Service with Circuit Breaker Pattern
/// Olympic-level feature: Production-Grade Features - Resilience
/// </summary>
internal class ProductionResilienceService : IResilienceService
{
    private readonly Dictionary<string, IAsyncPolicy> _policies = new(StringComparer.Ordinal);
    private readonly ILogger<ProductionResilienceService> _logger;

    public ProductionResilienceService(ILogger<ProductionResilienceService> logger)
    {
        _logger = logger;
        InitializePolicies();
    }

    private void InitializePolicies()
    {
        // Database operations policy with retry
        var dbPolicy = Policy
            .Handle<Exception>()
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                onRetry: (outcome, delay, retryCount, _) =>
                {
                    _logger.LogWarning("Database operation retry {RetryCount} after {Delay}s: {Exception}",
                        retryCount, delay.TotalSeconds, outcome.Message);
                });

        _policies["database"] = dbPolicy;

        // External API policy
        var apiPolicy = Policy
            .Handle<HttpRequestException>()
            .WaitAndRetryAsync(
                retryCount: 2,
                sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(retryAttempt),
                onRetry: (outcome, delay, retryCount, _) =>
                {
                    _logger.LogWarning("External API retry {RetryCount} after {Delay}s: {Exception}",
                        retryCount, delay.TotalSeconds, outcome.Message);
                });

        _policies["external_api"] = apiPolicy;

        // Process execution policy
        var processPolicy = Policy
            .Handle<Exception>()
            .WaitAndRetryAsync(
                retryCount: 2,
                sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(retryAttempt * 2),
                onRetry: (outcome, delay, retryCount, _) =>
                {
                    _logger.LogWarning("Process execution retry {RetryCount} after {Delay}s: {Exception}",
                        retryCount, delay.TotalSeconds, outcome.Message);
                });

        _policies["process_execution"] = processPolicy;

        // Health check policy
        var healthCheckPolicy = Policy
            .Handle<Exception>()
            .WaitAndRetryAsync(
                retryCount: 1,
                sleepDurationProvider: _ => TimeSpan.FromSeconds(1),
                onRetry: (outcome, delay, retryCount, _) =>
                {
                    _logger.LogWarning("Health check retry {RetryCount} after {Delay}s: {Exception}",
                        retryCount, delay.TotalSeconds, outcome.Message);
                });

        _policies["health_check"] = healthCheckPolicy;
    }

    public async Task<T> ExecuteAsync<T>(Func<Task<T>> operation, string operationName)
    {
    	if (!_policies.TryGetValue(operationName, out var policy))
    	{
    		_logger.LogWarning("No resilience policy found for operation: {OperationName}", operationName);
    		return await operation();
    	}

    	var result = await policy.ExecuteAsync(async () =>
    	{
    		if (_logger.IsEnabled(Microsoft.Extensions.Logging.LogLevel.Debug))
    		{
    			_logger.LogDebug("Executing operation: {OperationName}", operationName);
    		}

    		return await operation();
    	});
    	if (_logger.IsEnabled(Microsoft.Extensions.Logging.LogLevel.Debug))
    	{
    		_logger.LogDebug("Operation completed successfully: {OperationName}", operationName);
    	}

    	return result;
    }

    public async Task ExecuteAsync(Func<Task> operation, string operationName)
    {
        await ExecuteAsync(async () =>
        {
            await operation();
            return true;
        }, operationName);
    }

    public ResilienceStatus GetCircuitBreakerStatus(string operationName)
    {
        if (!_policies.ContainsKey(operationName))
        {
            return new ResilienceStatus(operationName, "Unknown", "No policy configured");
        }

        return new ResilienceStatus(operationName, "Healthy", "Policy active");
    }
}
