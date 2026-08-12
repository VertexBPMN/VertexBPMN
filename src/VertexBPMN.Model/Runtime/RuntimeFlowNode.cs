namespace VertexBPMN.Domain.Model.Runtime;

public sealed record RuntimeFlowNode(
    string Id,
    string Type,
    string? ParentSubprocessId,
    bool IsMultiInstance,
    bool IsMultiInstanceSequential,
    bool IsEventSubprocess,
    bool IsDefaultGatewayTarget
);