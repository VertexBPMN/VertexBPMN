// for ObsoleteAttribute

using System.Xml.Linq;
using VertexBPMN.Domain.Model.Runtime;

namespace VertexBPMN.Domain.Model.Bpmn;

public record MultiInstanceLoopCharacteristics(bool IsSequential, int? LoopCardinality, string? Collection, string? ElementVariable, string? CompletionCondition, string? InputElement = null, string? OutputElement = null)
	: LoopCharacteristics("multiInstance");
