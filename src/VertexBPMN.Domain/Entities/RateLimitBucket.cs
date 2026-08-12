namespace VertexBPMN.Domain.Entities;

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