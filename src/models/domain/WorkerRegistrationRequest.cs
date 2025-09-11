namespace VertexBPMN.Core.Domain;

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