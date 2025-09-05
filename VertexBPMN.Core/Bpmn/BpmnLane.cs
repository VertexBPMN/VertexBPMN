namespace VertexBPMN.Core.Bpmn;

public record BpmnLane(string Id, string Name, IReadOnlyList<string> FlowNodeRefs);