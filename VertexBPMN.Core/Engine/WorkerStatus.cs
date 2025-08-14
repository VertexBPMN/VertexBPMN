namespace VertexBPMN.Core.Engine;

/// <summary>
/// Worker status information
/// </summary>
public record WorkerStatus(
    int CurrentLoad,
    double CpuUsage,
    double MemoryUsage,
    int ActiveTasks,
    DateTime Timestamp
);