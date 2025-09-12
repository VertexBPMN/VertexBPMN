using System;

namespace VertexBPMN.Domain;

/// <summary>
/// Load balancing configuration
/// </summary>
public record LoadBalancingConfig
{
    public LoadBalancingStrategy Strategy { get; init; } = LoadBalancingStrategy.LeastLoaded;
    public int MinimumWorkers { get; init; } = 1;
    public int MaximumWorkers { get; init; } = 10;
    public TimeSpan WorkerTimeout { get; init; } = TimeSpan.FromMinutes(2);
    public bool AutoRebalance { get; init; } = true;
    public TimeSpan RebalanceInterval { get; init; } = TimeSpan.FromMinutes(5);
    public double LoadThreshold { get; init; } = 0.8; // 80%
}