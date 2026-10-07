// for ObsoleteAttribute

using System.Xml.Linq;
using VertexBPMN.Domain.Model.Runtime;

namespace VertexBPMN.Domain.Model.Bpmn;

public sealed record CompensationEventDefinition(string? ActivityRef, bool WaitForCompletion = true) : EventDefinition("compensation");
