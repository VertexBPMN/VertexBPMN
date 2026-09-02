namespace VertexBPMN.Domain.Entities;

/// <summary>
/// Worker heartbeat request
/// </summary>
public record WorkerHeartbeatRequest(
    int CurrentLoad
);