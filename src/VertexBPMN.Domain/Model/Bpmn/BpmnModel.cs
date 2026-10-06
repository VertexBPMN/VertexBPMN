// for ObsoleteAttribute

using System.Xml.Linq;
using VertexBPMN.Domain.Model.Runtime;

namespace VertexBPMN.Domain.Model.Bpmn;

public record BpmnModel(
	string ProcessId,
	string Name,
	IReadOnlyList<BpmnEvent>? Events = null,
	IReadOnlyList<BpmnGateway>? Gateways = null,
	IReadOnlyList<BpmnSubprocess>? Subprocesses = null,
	IReadOnlyList<BpmnSequenceFlow>? SequenceFlows = null,
	IReadOnlyList<BpmnTask>? Tasks = null,
	IReadOnlyList<BpmnDataObject>? DataObjects = null,
	IReadOnlyList<BpmnDataObjectReference>? DataObjectReferences = null,
	IReadOnlyList<BpmnDataStore>? DataStores = null,
	IReadOnlyList<BpmnDataStoreReference>? DataStoreReferences = null,
	IReadOnlyList<BpmnProperty>? Properties = null,
	IReadOnlyList<BpmnActivityIo>? ActivityIo = null,
	IReadOnlyList<BpmnMessage>? Messages = null,
	IReadOnlyList<BpmnSignal>? Signals = null,
	IReadOnlyList<BpmnError>? Errors = null,
	IReadOnlyList<BpmnEscalation>? Escalations = null,
	IReadOnlyList<string>? Diagnostics = null,
	IReadOnlyList<BpmnShape>? Shapes = null,
	IReadOnlyList<BpmnEdge>? Edges = null,
	IReadOnlyList<BpmnParticipant>? Participants = null,
	IReadOnlyList<BpmnLane>? Lanes = null,
	IReadOnlyList<BpmnMessageFlow>? MessageFlows = null,
	IReadOnlyList<BpmnTextAnnotation>? TextAnnotations = null,
	IReadOnlyList<BpmnAssociation>? Associations = null,
	IReadOnlyList<BpmnGroup>? Groups = null,
	Dictionary<string, object>? ProcessVariables = null,
	IEnumerable<object>? Activities = null,
	BpmnRawMetadata? RawMetadata = null
)
{
	public BpmnModel(string processId, string name, IReadOnlyList<BpmnEvent> bpmnEvents, IReadOnlyList<BpmnTask> bpmnTasks, IReadOnlyList<BpmnGateway> bpmnGateways, IReadOnlyList<BpmnSequenceFlow> bpmnSequenceFlows, IReadOnlyList<BpmnSubprocess> bpmnSubprocesses)
		: this(processId, name, bpmnEvents, bpmnGateways, bpmnSubprocesses, bpmnSequenceFlows, bpmnTasks)
	{
	}

	public RuntimeProcessModel? Runtime { get; set; }
	public IReadOnlyList<ValidationDiagnostic>? ValidationDiagnostics { get; set; }
	public IReadOnlyList<BpmnDefinition> Definitions { get; set; } = new List<BpmnDefinition>();
	public string Id => ProcessId;
}
