namespace VertexBPMN.Domain.Model.Runtime;

public sealed record RuntimeProcessModel(
    string ProcessId,
    IReadOnlyList<RuntimeFlowNode> FlowNodes,
    IReadOnlyList<RuntimeSequenceFlow> SequenceFlows,
    IReadOnlyDictionary<string, IReadOnlyDictionary<string,string>>? VendorExtensions,
    IReadOnlyDictionary<string, RuntimeScriptTask>? ScriptTasks,
    IReadOnlyDictionary<string, string>? PotentialOwners // NEW: userTask potentialOwner expressions
);