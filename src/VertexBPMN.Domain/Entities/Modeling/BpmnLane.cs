using System.Collections.Generic;

namespace VertexBPMN.Domain.Modeling;

public record BpmnLane(string Id, string Name, IReadOnlyList<string> FlowNodeRefs);