namespace VertexBPMN.Core.Engine;

/// <summary>
/// Worker node for distributed execution
/// </summary>
public record WorkerNode(
    string Id,
    string HostName,
    int Port,
    DateTime LastHeartbeat,
    List<string> SupportedNodeTypes,
    int CurrentLoad,
    int MaxCapacity
);