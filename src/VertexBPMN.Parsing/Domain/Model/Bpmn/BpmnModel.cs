// for ObsoleteAttribute

namespace VertexBPMN.Domain.Model.Bpmn;

public record BpmnEvent(string Id,string Type,IReadOnlyList<EventDefinition> Definitions,string? SubprocessId,Dictionary<string,string>? ExtensionAttributes=null);
public record BpmnGateway(string Id,string Type,string? DefaultFlowId,string? SubprocessId,Dictionary<string,string>? ExtensionAttributes=null);
public record BpmnSubprocess(string Id,bool IsEventSubprocess,bool IsTransaction,LoopCharacteristics? Loop,string? SubprocessId,Dictionary<string,string>? ExtensionAttributes=null,IReadOnlyList<string>? ChildFlowNodeIds=null,IReadOnlyList<string>? ChildSequenceFlowIds=null);
public record BpmnSequenceFlow(string Id,string SourceRef,string TargetRef,bool IsDefault,string? ConditionExpression,string? SubprocessId,Dictionary<string,string>? ExtensionAttributes=null,int? Priority=null);
public record BpmnTask(string Id,string Type,string? SubprocessId,  Dictionary<string,string>? Attributes=null, string? Implementation = null)
{
    public string Name { get; init; } = string.Empty;
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
public record BpmnAssociationArtifact(string Id,string SourceRef,string TargetRef,string? Direction);
public record BpmnGroup(string Id,string? CategoryValueRef);
public abstract record EventDefinition(string Kind);
public sealed record TimerEventDefinition(string? TimeDate, string? TimeDuration, string? TimeCycle) : EventDefinition("timer");
public sealed record MessageEventDefinition(string MessageRef, string? CorrelationKey) : EventDefinition("message");
public sealed record SignalEventDefinition(string SignalRef) : EventDefinition("signal");
public sealed record ErrorEventDefinition(string ErrorRef, string? ErrorCode = null) : EventDefinition("error");
public sealed record EscalationEventDefinition(string EscalationRef) : EventDefinition("escalation");
public sealed record LinkEventDefinition(string Name, string? Target = null) : EventDefinition("link");
public sealed record ConditionalEventDefinition(string Condition) : EventDefinition("conditional");
public sealed record CompensationEventDefinition(string? ActivityRef) : EventDefinition("compensation");
public sealed record CancelEventDefinition() : EventDefinition("cancel");
public sealed record TerminateEventDefinition() : EventDefinition("terminate");
public abstract record LoopCharacteristics(string Kind);
public record StandardLoopCharacteristics(string? LoopCondition, bool TestBefore, int? LoopMaximum) : LoopCharacteristics("standard");
public record MultiInstanceLoopCharacteristics(bool IsSequential, int? LoopCardinality, string? Collection, string? ElementVariable, string? CompletionCondition, string? InputElement = null, string? OutputElement = null)
    : LoopCharacteristics("multiInstance");


public record BpmnModel(
    string ProcessId,
    IReadOnlyList<BpmnEvent> Events,
    IReadOnlyList<BpmnGateway> Gateways,
    IReadOnlyList<BpmnSubprocess> Subprocesses,
    IReadOnlyList<BpmnSequenceFlow> SequenceFlows,
    IReadOnlyList<BpmnTask> Tasks,
    IReadOnlyList<BpmnDataObject> DataObjects,
    IReadOnlyList<BpmnDataObjectReference> DataObjectReferences,
    IReadOnlyList<BpmnDataStore> DataStores,
    IReadOnlyList<BpmnDataStoreReference> DataStoreReferences,
    IReadOnlyList<BpmnProperty> Properties,
    IReadOnlyList<BpmnActivityIo> ActivityIo,
    IReadOnlyList<BpmnMessage> Messages,
    IReadOnlyList<BpmnSignal> Signals,
    IReadOnlyList<BpmnError> Errors,
    IReadOnlyList<BpmnEscalation> Escalations,
    IReadOnlyList<string> Diagnostics,
    IReadOnlyList<BpmnShape>? Shapes = null,
    IReadOnlyList<BpmnEdge>? Edges = null,
    IReadOnlyList<BpmnParticipant>? Participants = null,
    IReadOnlyList<BpmnLane>? Lanes = null,
    IReadOnlyList<BpmnMessageFlow>? MessageFlows = null,
    IReadOnlyList<BpmnTextAnnotation>? TextAnnotations = null,
    IReadOnlyList<BpmnAssociationArtifact>? Associations = null,
    IReadOnlyList<BpmnGroup>? Groups = null,
    IDictionary<string, object>? ProcessVariables = null,
    IEnumerable<object>? Activities = null
);
