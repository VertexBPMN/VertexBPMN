// for ObsoleteAttribute

using System.Xml.Linq;
using VertexBPMN.Domain.Model.Runtime;

namespace VertexBPMN.Domain.Model.Bpmn;

public record BpmnEvent(string Id, string Type, IReadOnlyList<EventDefinition>? Definitions = null, string? SubprocessId = null, Dictionary<string, string>? Attributes = null)
{
	public string? ProcessId { get; init; }
	public Dictionary<string, string>? ExtensionAttributes => Attributes;
	public string Name => Attributes?.TryGetValue("name", out var name) == true ? name : string.Empty;
	public string AttachedToRef => Attributes?.TryGetValue("attachedToRef", out var attachedToRef) == true ? attachedToRef : string.Empty;
	public bool CancelActivity => Attributes?.TryGetValue("cancelActivity", out var value) != true ||
		!bool.TryParse(value, out var parsed) || parsed;
	public bool IsInterrupting => Attributes?.TryGetValue("isInterrupting", out var value) != true ||
		!bool.TryParse(value, out var parsed) || parsed;
	public bool IsCompensation => Attributes?.TryGetValue("isCompensation", out var value) == true &&
		bool.TryParse(value, out var parsed) && parsed;
	public string? EventDefinitionType => Definitions is { Count: > 0 } ? Definitions[0].Kind : null;
}
