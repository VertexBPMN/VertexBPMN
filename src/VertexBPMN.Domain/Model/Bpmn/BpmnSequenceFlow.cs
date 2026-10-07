// for ObsoleteAttribute

using System.Xml.Linq;
using VertexBPMN.Domain.Model.Runtime;

namespace VertexBPMN.Domain.Model.Bpmn;

public record BpmnSequenceFlow(string Id, string SourceRef, string TargetRef, bool IsDefault = false, string? ConditionExpression = null, string? SubprocessId = null, Dictionary<string, string>? Attributes = null, int? Priority = null)
{
	public string? ProcessId { get; init; }
	public Dictionary<string, string>? ExtensionAttributes => Attributes;
	public string? ConditionExpressionLanguage =>
		Attributes?.TryGetValue("conditionExpressionLanguage", out var language) == true
			? language
			: null;
	public string Name { get; init; } = string.Empty;
}
