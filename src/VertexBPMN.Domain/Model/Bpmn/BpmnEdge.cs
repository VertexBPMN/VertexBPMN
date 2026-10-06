// for ObsoleteAttribute

using System.Xml.Linq;
using VertexBPMN.Domain.Model.Runtime;

namespace VertexBPMN.Domain.Model.Bpmn;

public record BpmnEdge(string Id, string BpmnElementId, IReadOnlyList<(double X, double Y)> Waypoints);
