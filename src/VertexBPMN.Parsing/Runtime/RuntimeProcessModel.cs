using System.Collections.ObjectModel;

namespace VertexBPMN.Parsing;

public sealed record RuntimeProcessModel(
    string ProcessId,
    IReadOnlyList<RuntimeFlowNode> FlowNodes,
    IReadOnlyList<RuntimeSequenceFlow> SequenceFlows,
    IReadOnlyDictionary<string, IReadOnlyDictionary<string,string>>? VendorExtensions,
    IReadOnlyDictionary<string, RuntimeScriptTask>? ScriptTasks,
    IReadOnlyDictionary<string, string>? PotentialOwners // NEW: userTask potentialOwner expressions
);

public sealed record RuntimeFlowNode(
    string Id,
    string Type,
    string? ParentSubprocessId,
    bool IsMultiInstance,
    bool IsMultiInstanceSequential,
    bool IsEventSubprocess,
    bool IsDefaultGatewayTarget
);

public sealed record RuntimeSequenceFlow(
    string Id,
    string SourceId,
    string TargetId,
    bool IsDefault
);

public sealed record RuntimeScriptTask(
    string ScriptFormat,
    string ScriptBody,
    string? ResultVariable
);