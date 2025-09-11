using Microsoft.AspNetCore.Mvc.Filters;
using System.Collections.Concurrent;

namespace VertexBPMN.Api.Services;

/// <summary>
/// Production-Grade Rate Limiting Service
/// Olympic-level feature: Production-Grade Features - Rate Limiting
/// </summary>
public interface IRateLimitingService
{
    bool IsAllowed(string identifier, string rateLimitPolicy);
    RateLimitInfo GetRateLimitInfo(string identifier, string rateLimitPolicy);
    void ResetRateLimit(string identifier, string rateLimitPolicy);
}

public class ProductionRateLimitingService : IRateLimitingService
{
    private readonly ConcurrentDictionary<string, RateLimitBucket> _buckets = new();
    private readonly ILogger<ProductionRateLimitingService> _logger;
    private readonly Dictionary<string, RateLimitPolicy> _policies;

    public ProductionRateLimitingService(ILogger<ProductionRateLimitingService> logger)
    {
        _logger = logger;
        _policies = InitializePolicies();
        
        // Start cleanup timer
        _ = Task.Run(CleanupExpiredBuckets);
    }

    private Dictionary<string, RateLimitPolicy> InitializePolicies()
    {
        return new Dictionary<string, RateLimitPolicy>
        {
            ["default"] = new RateLimitPolicy(100, TimeSpan.FromMinutes(1)),      // 100 requests per minute
            ["authentication"] = new RateLimitPolicy(10, TimeSpan.FromMinutes(5)), // 10 auth attempts per 5 minutes
            ["api_creation"] = new RateLimitPolicy(20, TimeSpan.FromMinutes(1)),   // 20 creations per minute
            ["heavy_operations"] = new RateLimitPolicy(5, TimeSpan.FromMinutes(1)), // 5 heavy ops per minute
            ["process_execution"] = new RateLimitPolicy(50, TimeSpan.FromMinutes(1)), // 50 executions per minute
            ["monitoring"] = new RateLimitPolicy(200, TimeSpan.FromMinutes(1)),    // 200 monitoring calls per minute
            ["premium"] = new RateLimitPolicy(500, TimeSpan.FromMinutes(1)),       // Premium users: 500 per minute
            ["admin"] = new RateLimitPolicy(1000, TimeSpan.FromMinutes(1))         // Admin users: 1000 per minute
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
            _logger.LogWarning("Rate limit exceeded for {Identifier} with policy {Policy}", 
                identifier, rateLimitPolicy);
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
        if (_buckets.TryRemove(bucketKey, out _))
        {
            _logger.LogInformation("Rate limit reset for {Identifier} with policy {Policy}", 
                identifier, rateLimitPolicy);
        }
    }

    private async Task CleanupExpiredBuckets()
    {
        while (true)
        {
            try
            {
                var expiredKeys = _buckets
                    .Where(kvp => kvp.Value.IsExpired)
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var key in expiredKeys)
                {
                    _buckets.TryRemove(key, out _);
                }

                if (expiredKeys.Count > 0)
                {
                    _logger.LogDebug("Cleaned up {Count} expired rate limit buckets", expiredKeys.Count);
                }

                await Task.Delay(TimeSpan.FromMinutes(1));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during rate limit bucket cleanup");
                await Task.Delay(TimeSpan.FromMinutes(5));
            }
        }
    }
}

/// <summary>
/// Rate limiting action filter attribute
/// </summary>
public class RateLimitAttribute : ActionFilterAttribute
{
    private readonly string _policy;

    public RateLimitAttribute(string policy = "default")
    {
        _policy = policy;
    }

    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var rateLimitingService = context.HttpContext.RequestServices.GetRequiredService<IRateLimitingService>();
        
        // Get identifier (IP address or user ID)
        var identifier = GetIdentifier(context.HttpContext);
        
        if (!rateLimitingService.IsAllowed(identifier, _policy))
        {
            var rateLimitInfo = rateLimitingService.GetRateLimitInfo(identifier, _policy);
            
            context.HttpContext.Response.StatusCode = 429; // Too Many Requests
            context.HttpContext.Response.Headers["X-RateLimit-Limit"] = rateLimitInfo.Limit.ToString();
            context.HttpContext.Response.Headers["X-RateLimit-Remaining"] = rateLimitInfo.Remaining.ToString();
            context.HttpContext.Response.Headers["X-RateLimit-Reset"] = rateLimitInfo.ResetTime.ToString("O");
            context.HttpContext.Response.Headers["Retry-After"] = ((int)rateLimitInfo.WindowDuration.TotalSeconds).ToString();
            
            await context.HttpContext.Response.WriteAsync("Rate limit exceeded. Please try again later.");
            return;
        }

        // Add rate limit headers to successful responses
        var info = rateLimitingService.GetRateLimitInfo(identifier, _policy);
        context.HttpContext.Response.Headers["X-RateLimit-Limit"] = info.Limit.ToString();
        context.HttpContext.Response.Headers["X-RateLimit-Remaining"] = info.Remaining.ToString();
        context.HttpContext.Response.Headers["X-RateLimit-Reset"] = info.ResetTime.ToString("O");

        await next();
    }

    private string GetIdentifier(HttpContext context)
    {
        // Try to get user ID first
        var userId = context.User?.Identity?.Name;
        if (!string.IsNullOrEmpty(userId))
        {
            return $"user:{userId}";
        }

        // Fall back to IP address
        var ipAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        
        // Check for forwarded IP
        if (context.Request.Headers.TryGetValue("X-Forwarded-For", out var forwardedFor))
        {
            ipAddress = forwardedFor.FirstOrDefault()?.Split(',')[0].Trim() ?? ipAddress;
        }
        else if (context.Request.Headers.TryGetValue("X-Real-IP", out var realIp))
        {
            ipAddress = realIp.FirstOrDefault() ?? ipAddress;
        }

        return $"ip:{ipAddress}";
    }
}

/// <summary>
/// Rate limit policy configuration
/// </summary>
public record RateLimitPolicy(int RequestLimit, TimeSpan TimeWindow);

/// <summary>
/// Rate limit bucket for tracking requests
/// </summary>
public class RateLimitBucket
{
    private readonly RateLimitPolicy _policy;
    private readonly object _lock = new();
    private int _requestCount;
    private DateTime _windowStart;

    public RateLimitBucket(RateLimitPolicy policy)
    {
        _policy = policy;
        _windowStart = DateTime.UtcNow;
        _requestCount = 0;
    }

    public bool TryConsume()
    {
        lock (_lock)
        {
            var now = DateTime.UtcNow;
            
            // Reset window if expired
            if (now - _windowStart >= _policy.TimeWindow)
            {
                _windowStart = now;
                _requestCount = 0;
            }

            // Check if limit exceeded
            if (_requestCount >= _policy.RequestLimit)
            {
                return false;
            }

            _requestCount++;
            return true;
        }
    }

    public int RemainingRequests
    {
        get
        {
            lock (_lock)
            {
                var now = DateTime.UtcNow;
                
                // Reset window if expired
                if (now - _windowStart >= _policy.TimeWindow)
                {
                    return _policy.RequestLimit;
                }

                return Math.Max(0, _policy.RequestLimit - _requestCount);
            }
        }
    }

    public DateTime ResetTime
    {
        get
        {
            lock (_lock)
            {
                return _windowStart.Add(_policy.TimeWindow);
            }
        }
    }

    public bool IsExpired
    {
        get
        {
            lock (_lock)
            {
                var now = DateTime.UtcNow;
                return now - _windowStart >= _policy.TimeWindow.Add(TimeSpan.FromMinutes(5));
            }
        }
    }
}

/// <summary>
/// Rate limit information
/// </summary>
public class RateLimitInfo
{
    public string Identifier { get; set; } = string.Empty;
    public string Policy { get; set; } = string.Empty;
    public int Limit { get; set; }
    public int Remaining { get; set; }
    public DateTime ResetTime { get; set; }
    public TimeSpan WindowDuration { get; set; }
}
