// for ObsoleteAttribute

using System.Xml.Linq;
using VertexBPMN.Domain.Model.Runtime;

namespace VertexBPMN.Domain.Model.Bpmn;

public record BpmnTask(string Id, string Type, string? SubprocessId = null, Dictionary<string, string>? Attributes = null, string? Implementation = null)
{
	public ExternalTaskDefinition? ExternalTask => ExternalTaskDefinition.FromAttributes(Attributes);
	public LoopCharacteristics? Loop { get; init; }
	public string? ProcessId { get; init; }
	// Add Name property for compatibility
	public string Name { get; init; } = string.Empty;

	// Add Extensions property for compatibility with serializer
	public Dictionary<string, string>? Extensions => Attributes;
}
