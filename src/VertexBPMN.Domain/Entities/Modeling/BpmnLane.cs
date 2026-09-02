namespace VertexBPMN.Domain.Entities.Modeling;

public record BpmnLane(string Id, string Name, IReadOnlyList<string> FlowNodeRefs);