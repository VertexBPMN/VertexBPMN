using System;

namespace VertexBPMN.Domain;

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