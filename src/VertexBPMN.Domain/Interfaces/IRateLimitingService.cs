namespace VertexBPMN.Domain.Contracts;

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