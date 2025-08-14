namespace VertexBPMN.Core.Engine;

/// <summary>
/// Worker registration request
/// </summary>
public record WorkerRegistrationRequest(
    string WorkerId,
    string HostName,
    int Port,
    List<string> SupportedNodeTypes,
    int MaxCapacity
);