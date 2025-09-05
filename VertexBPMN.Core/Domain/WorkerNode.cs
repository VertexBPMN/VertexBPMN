namespace VertexBPMN.Core.Domain;

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
    int MaxCapacity,
    bool SupportsDmn = false,
    bool SupportsCmmn = false,
    bool SupportsBpmn = false);