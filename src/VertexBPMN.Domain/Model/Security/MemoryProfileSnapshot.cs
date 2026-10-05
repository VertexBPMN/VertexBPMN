namespace VertexBPMN.Domain.Model.Security;

/// <summary>
/// Process-wide managed-memory snapshot during a parse operation, not parser-exclusive
/// allocation or native working set. Peak is sampled and includes the final readings.
/// Use a quiescent process for comparative measurements.
/// </summary>
public sealed record MemoryProfileSnapshot
{
    public double InitialMemoryUsageMB { get; init; }
    public double PeakMemoryUsageMB { get; init; }
    public double FinalMemoryUsageMB { get; init; }
    public double RetainedMemoryMB { get; init; }
    public double TotalAllocatedMB { get; init; }
    public double GcCollectedMB { get; init; }
    public TimeSpan ParseDuration { get; init; }
    public double StringInterningEffectiveness { get; init; }
    public int ElementCount { get; init; }
}
