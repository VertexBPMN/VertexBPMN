namespace VertexBPMN.Domain.Model.Security;

/// <summary>
/// Snapshot of memory usage during a parse operation.
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