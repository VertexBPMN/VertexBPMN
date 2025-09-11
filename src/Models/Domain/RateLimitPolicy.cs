using System;

namespace VertexBPMN.Domain;

/// <summary>
/// Rate limit policy configuration
/// </summary>
public record RateLimitPolicy(int RequestLimit, TimeSpan TimeWindow);