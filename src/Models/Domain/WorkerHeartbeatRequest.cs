namespace VertexBPMN.Domain;

/// <summary>
/// Worker heartbeat request
/// </summary>
public record WorkerHeartbeatRequest(
    int CurrentLoad
);