namespace VertexBPMN.Parsing;

public sealed record RuntimeProcessModel(
    string ProcessId,
    IReadOnlyList<RuntimeFlowNode> FlowNodes,
    IReadOnlyList<RuntimeSequenceFlow> SequenceFlows,
    IReadOnlyDictionary<string, IReadOnlyDictionary<string,string>>? VendorExtensions // elementId -> flattened map (subset)
);

public sealed record RuntimeFlowNode(
    string Id,
    string Type,                  // task, userTask, startEvent, endEvent, gateway type, subprocess
    string? ParentSubprocessId,
    bool IsMultiInstance,
    bool IsMultiInstanceSequential,
    bool IsEventSubprocess,
    bool IsDefaultGatewayTarget   // convenience for filtering? (optional)
);

public sealed record RuntimeSequenceFlow(
    string Id,
    string SourceId,
    string TargetId,
    bool IsDefault
);