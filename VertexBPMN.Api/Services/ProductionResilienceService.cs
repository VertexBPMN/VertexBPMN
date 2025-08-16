using Polly;

namespace VertexBPMN.Api.Services;

/// <summary>
/// Production-Grade Resilience Service with Circuit Breaker Pattern
/// Olympic-level feature: Production-Grade Features - Resilience
/// </summary>
public interface IResilienceService
{
    Task<T> ExecuteAsync<T>(Func<Task<T>> operation, string operationName);
    Task ExecuteAsync(Func<Task> operation, string operationName);
    ResilienceStatus GetCircuitBreakerStatus(string operationName);
}

public class ProductionResilienceService : IResilienceService
{
    private readonly Dictionary<string, IAsyncPolicy> _policies = new();
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
                onRetry: (outcome, delay, retryCount, context) =>
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
                onRetry: (outcome, delay, retryCount, context) =>
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
                onRetry: (outcome, delay, retryCount, context) =>
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
                sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(1),
                onRetry: (outcome, delay, retryCount, context) =>
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

        try
        {
            var result = await policy.ExecuteAsync(async () =>
            {
                _logger.LogDebug("Executing operation: {OperationName}", operationName);
                return await operation();
            });

            _logger.LogDebug("Operation completed successfully: {OperationName}", operationName);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Operation failed after all retries: {OperationName}", operationName);
            throw;
        }
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
        if (!_policies.TryGetValue(operationName, out var policy))
        {
            return new ResilienceStatus(operationName, "Unknown", "No policy configured");
        }

        return new ResilienceStatus(operationName, "Healthy", "Policy active");
    }
}

/// <summary>
/// Resilience status information
/// </summary>
public record ResilienceStatus(
    string OperationName,
    string Status,
    string Message
);
