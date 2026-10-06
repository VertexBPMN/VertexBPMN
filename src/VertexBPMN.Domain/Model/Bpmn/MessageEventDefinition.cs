// for ObsoleteAttribute

using System.Xml.Linq;
using VertexBPMN.Domain.Model.Runtime;

namespace VertexBPMN.Domain.Model.Bpmn;

public sealed record MessageEventDefinition(string MessageRef, string? CorrelationKey) : EventDefinition("message");
