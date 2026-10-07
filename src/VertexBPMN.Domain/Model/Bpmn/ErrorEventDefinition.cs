// for ObsoleteAttribute

using System.Xml.Linq;
using VertexBPMN.Domain.Model.Runtime;

namespace VertexBPMN.Domain.Model.Bpmn;

public sealed record ErrorEventDefinition(string ErrorRef, string? ErrorCode = null) : EventDefinition("error");
