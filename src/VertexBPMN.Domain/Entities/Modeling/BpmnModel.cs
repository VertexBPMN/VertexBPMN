namespace VertexBPMN.Domain.Entities.Modeling;

public class BpmnModel
{
    public string Id { get; set; }
    public string Name { get; set; }

    public List<BpmnEvent> Events { get; set; }
    public List<BpmnTask> Tasks { get; set; }
    public List<BpmnGateway> Gateways { get; set; }
    public List<BpmnSubprocess> Subprocesses { get; set; }
    public List<BpmnSequenceFlow> SequenceFlows { get; set; }
    public List<BpmnLane> Lanes { get; set; }
    public List<BpmnDataObject> DataObjects { get; set; }
    public List<BpmnAssociation> Associations { get; set; }
    public List<BpmnTextAnnotation> TextAnnotations { get; set; }
    public List<BpmnParticipant> Participants { get; set; }
    public List<BpmnMessageFlow> MessageFlows { get; set; }
    public Dictionary<string, object>? ProcessVariables { get; set; }

    public string ProcessId => Id;
    public IEnumerable<object> Activities => Tasks.Cast<object>().Concat(Subprocesses);

    public BpmnModel()
    {
        Id = string.Empty;
        Name = string.Empty;
        Events = new();
        Tasks = new();
        Gateways = new();
        Subprocesses = new();
        SequenceFlows = new();
        Lanes = new();
        DataObjects = new();
        Associations = new();
        TextAnnotations = new();
        Participants = new();
        MessageFlows = new();
        ProcessVariables = new Dictionary<string, object>();
    }

    // NEW constructor matching the 6-argument usage in tests
    public BpmnModel(
        string id,
        string name,
        List<BpmnEvent> events,
        List<BpmnTask> tasks,
        List<BpmnGateway> gateways,
        List<BpmnSequenceFlow> sequenceFlows,
        List<BpmnSubprocess> subprocesses)
        : this()
    {
        Id = id;
        Name = name;
        Events = events ?? new();
        Tasks = tasks ?? new();
        Gateways = gateways ?? new();
        SequenceFlows = sequenceFlows ?? new();
        Subprocesses = subprocesses ?? new();
    }

    public BpmnModel(
        string id,
        string name,
        List<BpmnEvent> events,
        List<BpmnTask> tasks,
        List<BpmnGateway> gateways,
        List<BpmnSubprocess> subprocesses,
        List<BpmnSequenceFlow> sequenceFlows, Dictionary<string, object>? processVariables = null)
        : this()
    {
        Id = id;
        Name = name;
        Events = events ?? new();
        Tasks = tasks ?? new();
        Gateways = gateways ?? new();
        SequenceFlows = sequenceFlows ?? new();
        Subprocesses = subprocesses ?? new();
        ProcessVariables = processVariables ?? new Dictionary<string, object>();
    }

    // (Optional future expansion) Extended constructor could be added here if needed.
}
