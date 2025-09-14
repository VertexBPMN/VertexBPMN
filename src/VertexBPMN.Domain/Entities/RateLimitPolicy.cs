namespace VertexBPMN.Domain.Entities;

/// <summary>
/// Rate limit policy configuration
/// </summary>
public record RateLimitPolicy(int RequestLimit, TimeSpan TimeWindow);