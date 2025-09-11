namespace VertexBPMN.Core.Modeling;

public record BpmnLane(string Id, string Name, IReadOnlyList<string> FlowNodeRefs);