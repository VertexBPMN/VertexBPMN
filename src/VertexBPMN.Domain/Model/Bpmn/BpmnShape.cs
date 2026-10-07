// for ObsoleteAttribute

using System.Xml.Linq;
using VertexBPMN.Domain.Model.Runtime;

namespace VertexBPMN.Domain.Model.Bpmn;

public record BpmnShape(string Id, string BpmnElementId, double X, double Y, double Width, double Height);
