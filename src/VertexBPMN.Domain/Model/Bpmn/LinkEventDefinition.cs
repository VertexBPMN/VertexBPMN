// for ObsoleteAttribute

using System.Xml.Linq;
using VertexBPMN.Domain.Model.Runtime;

namespace VertexBPMN.Domain.Model.Bpmn;

public sealed record LinkEventDefinition(string Name, string? Target = null, IReadOnlyList<string>? Sources = null) : EventDefinition("link");
