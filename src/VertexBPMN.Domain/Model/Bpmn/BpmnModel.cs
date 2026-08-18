// for ObsoleteAttribute

using VertexBPMN.Domain.Model.Runtime;

namespace VertexBPMN.Domain.Model.Bpmn;

using System.Xml.Linq;

public record BpmnDefinition(string Id, string? Name, string? TargetNamespace, string? Expression);
public record BpmnEvent(string Id,string Type, IReadOnlyList<EventDefinition> Definitions = null, string? SubprocessId = null, Dictionary<string,string>? Attributes=null)
{
    public Dictionary<string, string>? ExtensionAttributes => Attributes;
    public string Name => Attributes?.TryGetValue("name", out var name) == true ? name : string.Empty;
    public string AttachedToRef => Attributes?.TryGetValue("attachedToRef", out var attachedToRef) == true ? attachedToRef : string.Empty;
    public bool CancelActivity => Attributes?.TryGetValue("cancelActivity", out var value) != true ||
        !bool.TryParse(value, out var parsed) || parsed;
    public bool IsInterrupting => Attributes?.TryGetValue("isInterrupting", out var value) != true ||
        !bool.TryParse(value, out var parsed) || parsed;
    public bool IsCompensation => Attributes?.TryGetValue("isCompensation", out var value) == true &&
        bool.TryParse(value, out var parsed) && parsed;
    public string? EventDefinitionType => Definitions is {Count: > 0} ? Definitions[0].Kind : null;
}
public record BpmnGateway(string Id,string Type,string? DefaultFlowId = null,string? SubprocessId = null, Dictionary<string,string>? ExtensionAttributes=null);
public record BpmnSubprocess(string Id,bool IsEventSubprocess,bool IsTransaction = false, LoopCharacteristics? Loop = null,string? SubprocessId = null,Dictionary<string,string>? Attributes=null,IReadOnlyList<string>? ChildFlowNodeIds=null,IReadOnlyList<string>? ChildSequenceFlowIds=null)
{
    public Dictionary<string, string>? ExtensionAttributes => Attributes;
    public bool IsMultiInstance => Loop is MultiInstanceLoopCharacteristics;
    public int LoopCardinality => Loop is MultiInstanceLoopCharacteristics multiInstance
        ? multiInstance.LoopCardinality.GetValueOrDefault(1)
        : 1;
    public bool IsSequential => (Loop is MultiInstanceLoopCharacteristics) && (Loop as MultiInstanceLoopCharacteristics).IsSequential;
}
public record BpmnSequenceFlow(string Id,string SourceRef,string TargetRef,bool IsDefault = false,string? ConditionExpression = null,string? SubprocessId = null,Dictionary<string,string>? Attributes=null,int? Priority=null)
{
    public Dictionary<string, string>? ExtensionAttributes => Attributes;
    public string Name { get; init; } = string.Empty;
}
public record BpmnTask(string Id,string Type, string? SubprocessId = null,  Dictionary<string,string>? Attributes=null, string? Implementation = null)
{
    // Add Name property for compatibility
    public string Name { get; init; } = string.Empty;

    // Add Extensions property for compatibility with serializer
    public Dictionary<string, string>? Extensions => Attributes;
}
public record BpmnDataObject(string Id,string? Name);
public record BpmnDataObjectReference(string Id,string DataObjectRef);
public record BpmnDataStore(string Id,string? Name);
public record BpmnDataStoreReference(string Id,string DataStoreRef);
public record BpmnProperty(string Id,string? Name);
public record BpmnDataInput(string Id,string? Name);
public record BpmnDataOutput(string Id,string? Name);
public record BpmnDataAssociation(string SourceRef,string TargetRef);
public record BpmnActivityIo(string ActivityId,IReadOnlyList<BpmnDataInput> DataInputs,IReadOnlyList<BpmnDataOutput> DataOutputs,IReadOnlyList<BpmnDataAssociation> InputAssociations,IReadOnlyList<BpmnDataAssociation> OutputAssociations);
public record BpmnMessage(string Id,string? Name);
public record BpmnSignal(string Id,string? Name);
public record BpmnError(string Id,string? Name,string? ErrorCode);
public record BpmnEscalation(string Id,string? Name,string? EscalationCode);
public record BpmnShape(string Id,string BpmnElementId,double X,double Y,double Width,double Height);
public record BpmnEdge(string Id,string BpmnElementId,IReadOnlyList<(double X,double Y)> Waypoints);
public record BpmnParticipant(string Id,string? Name,string? ProcessRef);
public record BpmnLane(string Id,string? Name,IReadOnlyList<string> FlowNodeRefs);
public record BpmnMessageFlow(string Id,string SourceRef,string TargetRef,string? Name);
public record BpmnTextAnnotation(string Id,string? Text);
public record BpmnAssociation(string Id,string SourceRef,string TargetRef,string? Direction);
public record BpmnGroup(string Id,string? CategoryValueRef);
public abstract record EventDefinition(string Kind, string? Id = null);
public sealed record TimerEventDefinition(string? TimeDate, string? TimeDuration, string? TimeCycle) : EventDefinition("timer");
public sealed record MessageEventDefinition(string MessageRef, string? CorrelationKey) : EventDefinition("message");
public sealed record SignalEventDefinition(string SignalRef) : EventDefinition("signal");
public sealed record ErrorEventDefinition(string ErrorRef, string? ErrorCode = null) : EventDefinition("error");
public sealed record EscalationEventDefinition(string EscalationRef) : EventDefinition("escalation");
public sealed record LinkEventDefinition(string Name, string? Target = null,IReadOnlyList<string>? Sources = null) : EventDefinition("link");
public sealed record ConditionalEventDefinition(string Condition) : EventDefinition("conditional");
public sealed record CompensationEventDefinition(string? ActivityRef, bool WaitForCompletion = true) : EventDefinition("compensation");
public sealed record CancelEventDefinition() : EventDefinition("cancel");
public sealed record TerminateEventDefinition() : EventDefinition("terminate");
public abstract record LoopCharacteristics(string Kind);
public record StandardLoopCharacteristics(string? LoopCondition, bool TestBefore, int? LoopMaximum) : LoopCharacteristics("standard");
public record MultiInstanceLoopCharacteristics(bool IsSequential, int? LoopCardinality, string? Collection, string? ElementVariable, string? CompletionCondition, string? InputElement = null, string? OutputElement = null)
    : LoopCharacteristics("multiInstance");

/// <summary>
/// Namespace prefix entry for strict roundtrip (Phase A).
/// </summary>
public sealed record NamespacePrefix(string Prefix,string Uri,bool Original=true);

/// <summary>
/// Per-element metadata captured for strict ordering & attribute preservation.
/// </summary>
public sealed record ElementMetadata(
    int OrderIndex,
    string ElementName,
    IReadOnlyDictionary<string,string> Attributes,
    bool HadCamundaCollection = false,
    bool HadZeebeInputCollection = false,
    bool HadLoopCardinality = false,
    bool HadCamundaElementVar = false,
    bool HadZeebeInputElement = false,
    bool HadZeebeOutputElement = false
);

/// <summary>
/// Raw metadata captured for strict roundtrip mode (Phase 1/2 + Phase A extensions).
/// RoundtripDirty indicates the model was mutated after parsing and a lossless emit may not be valid.
/// </summary>
public sealed record BpmnRawMetadata(
    IReadOnlyDictionary<string,string>? DefinitionsAttributes = null,
    IReadOnlyDictionary<string,string>? ProcessAttributes = null,
    IReadOnlyDictionary<string, IReadOnlyList<string>>? Incoming = null,
    IReadOnlyDictionary<string, IReadOnlyList<string>>? Outgoing = null,
    IReadOnlyDictionary<string,(string Raw,bool WasCData)>? SequenceFlowConditions = null,
    IReadOnlyDictionary<string,XElement>? RawExtensionElements = null,
    IReadOnlyDictionary<string, IReadOnlyList<XElement>>? RawEventDefinitions = null,
    IReadOnlyDictionary<string,XElement>? RawMultiInstance = null,
    IReadOnlyDictionary<string,string>? PriorityAttributeNamespace = null,
    IReadOnlyDictionary<string, IReadOnlyDictionary<string,string>>? FlowNodeAttributes = null,
    bool RoundtripDirty = false,
    IReadOnlyList<NamespacePrefix>? NamespacePrefixes = null,
    IReadOnlyDictionary<string, ElementMetadata>? ElementsMetadata = null,
    IReadOnlyList<XElement>? RawGlobalElements = null,
    IReadOnlyList<XElement>? RawArtifacts = null,
    IReadOnlyList<XElement>? RawLanes = null,
    IReadOnlyDictionary<string, IReadOnlyList<XElement>>? RawDocumentation = null,
    XElement? RawDiRoot = null,
    IReadOnlySet<string>? PartiallyDirtyElements = null,
    IReadOnlyDictionary<string,string>? GlobalElementKinds = null,
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>? VendorNormalizedExtensions = null,
    string? OriginalXml = null
);
public static class BpmnRoundtripUtil
{
    public static BpmnModel MarkDirty(BpmnModel model)
    {
        if (model.RawMetadata == null) return model;
        if (model.RawMetadata.RoundtripDirty) return model; // already marked
        var rm = model.RawMetadata with { RoundtripDirty = true };
        return model with { RawMetadata = rm };
    }

    public static BpmnModel MarkDirtyOnAnyChange(BpmnModel model, string elementId)
    {
        var diagnostics = model.Diagnostics.ToList();
        diagnostics.Add($"RT-Dirty:element:{elementId}");
        model = model with { Diagnostics = diagnostics };
        return MarkDirty(model);
    }

    public static BpmnModel ApplyAttributeChange(BpmnModel model, string elementId, string key, string value)
    {
        // Update task attributes if target is a task
        if (model.Tasks.FirstOrDefault(t => t.Id == elementId) is { } task)
        {
            var attrs = task.Attributes == null ? new Dictionary<string,string>() : new Dictionary<string,string>(task.Attributes);
            attrs[key] = value;
            if (key == "name") task = task with { Attributes = attrs, Name = value };
            else task = task with { Attributes = attrs };
            var tasks = model.Tasks.ToList();
            var idx = tasks.FindIndex(t => t.Id == elementId);
            tasks[idx] = task;
            model = model with { Tasks = tasks };
        }
        // (Could add similar handling for events, gateways etc.)
        return MarkDirtyOnAnyChange(model, elementId);
    }

    public static BpmnModel ApplyAttributeChangePartial(BpmnModel model, string elementId, string key, string value)
    {
        if (model.RawMetadata == null) return model; // nothing to do
        // mutate element (tasks only for now) without setting RoundtripDirty, track element id in PartiallyDirtyElements
        if (model.Tasks.FirstOrDefault(t => t.Id == elementId) is { } task)
        {
            var attrs = task.Attributes == null ? new Dictionary<string,string>() : new Dictionary<string,string>(task.Attributes);
            attrs[key] = value;
            task = key == "name" ? task with { Attributes = attrs, Name = value } : task with { Attributes = attrs };
            var tasks = model.Tasks.ToList();
            var idx = tasks.FindIndex(t => t.Id == elementId);
            tasks[idx] = task;
            var diagnostics = model.Diagnostics.ToList();
            diagnostics.Add($"RT-DirtyPartial:element:{elementId}");
            var dirtySet = model.RawMetadata.PartiallyDirtyElements != null ? new HashSet<string>(model.RawMetadata.PartiallyDirtyElements) : new HashSet<string>();
            dirtySet.Add(elementId);
            var rm = model.RawMetadata with { PartiallyDirtyElements = dirtySet }; // keep RoundtripDirty false
            model = model with { Tasks = tasks, Diagnostics = diagnostics, RawMetadata = rm };
        }
        return model;
    }
}

public enum GatewayDecisionKind
{
    Selected,
    DefaultSelected,
    NoOutgoingFlow
}

public sealed record GatewayDecision(
    GatewayDecisionKind Kind,
    BpmnSequenceFlow? Flow);
public record BpmnModel(
    string ProcessId,
    string Name,
    IReadOnlyList<BpmnEvent> Events = null,
    IReadOnlyList<BpmnGateway> Gateways = null,
    IReadOnlyList<BpmnSubprocess> Subprocesses = null,
    IReadOnlyList<BpmnSequenceFlow> SequenceFlows = null,
    IReadOnlyList<BpmnTask> Tasks = null,
    IReadOnlyList<BpmnDataObject> DataObjects = null,
    IReadOnlyList<BpmnDataObjectReference> DataObjectReferences = null,
    IReadOnlyList<BpmnDataStore> DataStores = null,
    IReadOnlyList<BpmnDataStoreReference> DataStoreReferences = null,
    IReadOnlyList<BpmnProperty> Properties = null,
    IReadOnlyList<BpmnActivityIo> ActivityIo = null,
    IReadOnlyList<BpmnMessage> Messages = null,
    IReadOnlyList<BpmnSignal> Signals = null,
    IReadOnlyList<BpmnError> Errors = null,
    IReadOnlyList<BpmnEscalation> Escalations = null,
    IReadOnlyList<string> Diagnostics = null,
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
