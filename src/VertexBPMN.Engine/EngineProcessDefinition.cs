namespace VertexBPMN.Engine;

public sealed record EngineProcessDefinition(
	string Key,
	IReadOnlyDictionary<string, EngineFlowNode> Nodes,
	IReadOnlyList<EngineSequenceFlow> SequenceFlows,
	IReadOnlyList<string> StartEventIds,
	IReadOnlyDictionary<string, IReadOnlyList<EngineSequenceFlow>> Outgoing,
	IReadOnlyDictionary<string, IReadOnlyList<EngineSequenceFlow>> Incoming,
	DateTime DeployedAt,
	IReadOnlyList<string> Diagnostics
);
