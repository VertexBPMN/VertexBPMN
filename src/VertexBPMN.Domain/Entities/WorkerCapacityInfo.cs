namespace VertexBPMN.Domain.Entities;

/// <summary>
/// Worker capacity information
/// </summary>
public record WorkerCapacityInfo(
    string WorkerId,
    bool IsAvailable,
    int CurrentLoad,
    int MaxCapacity,
    int AvailableCapacity,
    double UtilizationPercentage = 0
);