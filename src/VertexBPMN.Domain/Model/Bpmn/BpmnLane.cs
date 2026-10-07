// for ObsoleteAttribute

using System.Xml.Linq;
using VertexBPMN.Domain.Model.Runtime;

namespace VertexBPMN.Domain.Model.Bpmn;

public record BpmnLane(string Id, string? Name, IReadOnlyList<string> FlowNodeRefs);
