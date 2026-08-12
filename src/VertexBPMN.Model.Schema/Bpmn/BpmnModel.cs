
using VertexBPMN.Domain.Model.Bpmn.Validation;

namespace VertexBPMN.Domain.Model.Bpmn;

public record BpmnModel
{
    public string ProcessId { get; set; }
    public string Name { get; set; }
    public List<Event> Events { get; set; }
    public List<Gateway> Gateways { get; set; }
    public List<SubProcess> Subprocesses { get; set; }
    public List<SequenceFlow> SequenceFlows { get; set; }
    public List<Task> Tasks { get; set; }
    public List<DataObject> DataObjects { get; set; }
    public List<DataObjectReference> DataObjectReferences { get; set; }
    public List<DataStore> DataStores { get; set; }
    public List<DataStoreReference> DataStoreReferences { get; set; }
    public List<Property> Properties { get; set; }
    public List<Activity> ActivityIo { get; set; }
    public List<Message> Messages { get; set; }
    public List<Signal> Signals { get; set; }
    public List<Error> Errors { get; set; }
    public List<Escalation> Escalations { get; set; }
    public List<string> Diagnostics { get; set; }
    public List<BpmnShape>? Shapes { get; set; }
    public List<BpmnEdge>? Edges { get; set; }
    public List<BpmnLabelStyle>? LabelStyles { get; set; }
    public List<Participant>? Participants { get; set; }
    public List<Lane>? Lanes { get; set; }
    public List<MessageFlow>? MessageFlows { get; set; }
    public List<TextAnnotation>? TextAnnotations { get; set; }
    public List<Association>? Associations { get; set; }
    public List<Group>? Groups { get; set; }
    public Dictionary<string, object>? ProcessVariables { get; set; }
    public List<Activity>? Activities { get; set; }
    public List<Definitions> Definitions { get; set; } = [];
    public string Id => ProcessId;
    public Definitions ProcessDefinitions { get; set; }
    public List<ValidationDiagnostic> ValidationDiagnostics { get; set; } = [];
}