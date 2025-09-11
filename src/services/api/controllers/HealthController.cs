using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using VertexBPMN.Api.Services;

namespace VertexBPMN.Api.Controllers;

/// <summary>
/// Production Health and Monitoring Controller
/// Olympic-level feature: Production-Grade Features - Health and Monitoring
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    private readonly IHealthMonitoringService _healthMonitoring;
    private readonly IResilienceService _resilience;
    private readonly IRateLimitingService _rateLimiting;
    private readonly ILogger<HealthController> _logger;

    public HealthController(
        IHealthMonitoringService healthMonitoring,
        IResilienceService resilience,
        IRateLimitingService rateLimiting,
        ILogger<HealthController> logger)
    {
        _healthMonitoring = healthMonitoring;
        _resilience = resilience;
        _rateLimiting = rateLimiting;
        _logger = logger;
    }

    /// <summary>
    /// Basic health check endpoint
    /// </summary>
    [HttpGet]
    [RateLimit("monitoring")]
    public IActionResult GetHealth()
    {
        return Ok(new { status = "healthy", timestamp = DateTime.UtcNow });
    }

    /// <summary>
    /// Comprehensive health report
    /// </summary>
    [HttpGet("comprehensive")]
    [RateLimit("monitoring")]
    public async Task<IActionResult> GetComprehensiveHealth()
    {
        try
        {
            var report = await _resilience.ExecuteAsync(
                () => _healthMonitoring.GetComprehensiveHealthReportAsync(),
                "health_check");

            var statusCode = report.OverallStatus switch
            {
                "Healthy" => 200,
                "Degraded" => 200, // Still return 200 but with degraded status
                "Unhealthy" => 503,
                _ => 500
            };

            return StatusCode(statusCode, report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Comprehensive health check failed");
            return StatusCode(503, new { status = "unhealthy", error = ex.Message });
        }
    }

    /// <summary>
    /// Database health check
    /// </summary>
    [HttpGet("database")]
    [RateLimit("monitoring")]
    public async Task<IActionResult> GetDatabaseHealth()
    {
        try
        {
            var result = await _resilience.ExecuteAsync(
                () => _healthMonitoring.CheckDatabaseHealthAsync(),
                "database");

            return result.Status switch
            {
                HealthStatus.Healthy => Ok(result),
                HealthStatus.Degraded => Ok(result),
                HealthStatus.Unhealthy => StatusCode(503, result),
                _ => StatusCode(500, result)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database health check failed");
            return StatusCode(503, new { status = "unhealthy", error = ex.Message });
        }
    }

    /// <summary>
    /// Memory health check
    /// </summary>
    [HttpGet("memory")]
    [RateLimit("monitoring")]
    public async Task<IActionResult> GetMemoryHealth()
    {
        try
        {
            var result = await _healthMonitoring.CheckMemoryHealthAsync();
            
            return result.Status switch
            {
                HealthStatus.Healthy => Ok(result),
                HealthStatus.Degraded => Ok(result),
                HealthStatus.Unhealthy => StatusCode(503, result),
                _ => StatusCode(500, result)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Memory health check failed");
            return StatusCode(503, new { status = "unhealthy", error = ex.Message });
        }
    }

    /// <summary>
    /// Disk space health check
    /// </summary>
    [HttpGet("disk")]
    [RateLimit("monitoring")]
    public async Task<IActionResult> GetDiskHealth()
    {
        try
        {
            var result = await _healthMonitoring.CheckDiskSpaceHealthAsync();
            
            return result.Status switch
            {
                HealthStatus.Healthy => Ok(result),
                HealthStatus.Degraded => Ok(result),
                HealthStatus.Unhealthy => StatusCode(503, result),
                _ => StatusCode(500, result)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Disk health check failed");
            return StatusCode(503, new { status = "unhealthy", error = ex.Message });
        }
    }

    /// <summary>
    /// External services health check
    /// </summary>
    [HttpGet("external")]
    [RateLimit("monitoring")]
    public async Task<IActionResult> GetExternalServicesHealth()
    {
        try
        {
            var result = await _healthMonitoring.CheckExternalServicesHealthAsync();
            
            return result.Status switch
            {
                HealthStatus.Healthy => Ok(result),
                HealthStatus.Degraded => Ok(result),
                HealthStatus.Unhealthy => StatusCode(503, result),
                _ => StatusCode(500, result)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "External services health check failed");
            return StatusCode(503, new { status = "unhealthy", error = ex.Message });
        }
    }

    /// <summary>
    /// System metrics endpoint
    /// </summary>
    [HttpGet("metrics")]
    [RateLimit("monitoring")]
    public async Task<IActionResult> GetSystemMetrics()
    {
        try
        {
            var metrics = await _healthMonitoring.GetSystemMetricsAsync();
            return Ok(metrics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "System metrics collection failed");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Circuit breaker status
    /// </summary>
    [HttpGet("circuit-breakers")]
    [RateLimit("monitoring")]
    public IActionResult GetCircuitBreakerStatus()
    {
        try
        {
            var operations = new[] { "database", "external_api", "process_execution" };
            var statuses = operations.Select(op => _resilience.GetCircuitBreakerStatus(op));
            
            return Ok(statuses);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Circuit breaker status check failed");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Rate limiting status for a specific identifier
    /// </summary>
    [HttpGet("rate-limits/{identifier}")]
    [RateLimit("monitoring")]
    public IActionResult GetRateLimitStatus(string identifier)
    {
        try
        {
            var policies = new[] { "default", "authentication", "api_creation", "heavy_operations" };
            var rateLimits = policies.Select(policy => 
                _rateLimiting.GetRateLimitInfo(identifier, policy));
            
            return Ok(rateLimits);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Rate limit status check failed for {Identifier}", identifier);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Readiness probe for Kubernetes
    /// </summary>
    [HttpGet("ready")]
    [RateLimit("monitoring")]
    public async Task<IActionResult> GetReadiness()
    {
        try
        {
            // Check critical dependencies
            var dbHealth = await _healthMonitoring.CheckDatabaseHealthAsync();
            
            if (dbHealth.Status == HealthStatus.Unhealthy)
            {
                return StatusCode(503, new { status = "not ready", reason = "database unhealthy" });
            }

            return Ok(new { status = "ready", timestamp = DateTime.UtcNow });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Readiness check failed");
            return StatusCode(503, new { status = "not ready", error = ex.Message });
        }
    }

    /// <summary>
    /// Liveness probe for Kubernetes
    /// </summary>
    [HttpGet("live")]
    [RateLimit("monitoring")]
    public IActionResult GetLiveness()
    {
        // Simple liveness check - if we can respond, we're alive
        return Ok(new { status = "alive", timestamp = DateTime.UtcNow });
    }

    /// <summary>
    /// Force garbage collection (admin only)
    /// </summary>
    [HttpPost("gc")]
    [RateLimit("admin")]
    public IActionResult ForceGarbageCollection()
    {
        try
        {
            var beforeMB = GC.GetTotalMemory(false) / 1024 / 1024;
            
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            
            var afterMB = GC.GetTotalMemory(false) / 1024 / 1024;
            var freedMB = beforeMB - afterMB;
            
            _logger.LogInformation("Manual GC completed. Freed {FreedMB}MB memory", freedMB);
            
            return Ok(new 
            { 
                status = "completed",
                before_mb = beforeMB,
                after_mb = afterMB,
                freed_mb = freedMB,
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Manual garbage collection failed");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Reset rate limit for specific identifier (admin only)
    /// </summary>
    [HttpPost("rate-limits/{identifier}/reset")]
    [RateLimit("admin")]
    public IActionResult ResetRateLimit(string identifier, [FromQuery] string policy = "default")
    {
        try
        {
            _rateLimiting.ResetRateLimit(identifier, policy);
            _logger.LogInformation("Rate limit reset for {Identifier} with policy {Policy}", identifier, policy);
            
            return Ok(new 
            { 
                status = "reset",
                identifier,
                policy,
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Rate limit reset failed for {Identifier}", identifier);
            return StatusCode(500, new { error = ex.Message });
        }
    }
}
