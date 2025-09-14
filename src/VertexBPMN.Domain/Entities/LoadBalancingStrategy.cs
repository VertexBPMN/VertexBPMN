namespace VertexBPMN.Domain.Entities;

/// <summary>
/// Load balancing strategies
/// </summary>
public enum LoadBalancingStrategy
{
    RoundRobin,
    LeastLoaded,
    WeightedRoundRobin,
    Random
}