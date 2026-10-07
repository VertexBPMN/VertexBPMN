// for ObsoleteAttribute

using System.Xml.Linq;
using VertexBPMN.Domain.Model.Runtime;

namespace VertexBPMN.Domain.Model.Bpmn;

public record BpmnGateway(string Id, string Type, string? DefaultFlowId = null, string? SubprocessId = null, Dictionary<string, string>? ExtensionAttributes = null)
{
	public string? ProcessId { get; init; }
}
