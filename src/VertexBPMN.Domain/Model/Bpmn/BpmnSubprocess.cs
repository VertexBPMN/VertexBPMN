// for ObsoleteAttribute

using System.Xml.Linq;
using VertexBPMN.Domain.Model.Runtime;

namespace VertexBPMN.Domain.Model.Bpmn;

public record BpmnSubprocess(string Id, bool IsEventSubprocess, bool IsTransaction = false, LoopCharacteristics? Loop = null, string? SubprocessId = null, Dictionary<string, string>? Attributes = null, IReadOnlyList<string>? ChildFlowNodeIds = null, IReadOnlyList<string>? ChildSequenceFlowIds = null)
{
	public string? ProcessId { get; init; }
	public Dictionary<string, string>? ExtensionAttributes => Attributes;
	public bool IsMultiInstance => Loop is MultiInstanceLoopCharacteristics;
	public int LoopCardinality => Loop is MultiInstanceLoopCharacteristics multiInstance
		? multiInstance.LoopCardinality ?? 1 : 1;
	public bool IsSequential => Loop is MultiInstanceLoopCharacteristics { IsSequential: true };
}
