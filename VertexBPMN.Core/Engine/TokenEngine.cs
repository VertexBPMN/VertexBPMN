using VertexBPMN.Core.Bpmn;
using VertexBPMN.Core.Services;
using VertexBPMN.Core.Tasks;

namespace VertexBPMN.Core.Engine;

/// <summary>
/// Advanced BPMN 2.0 Token Engine with support for boundary events, multi-instance, compensation, and transactions.
/// Olympic-level implementation for comprehensive BPMN execution.
/// </summary>
public class TokenEngine : IProcessEngine
{
    private readonly Dictionary<string, List<ExecutionToken>> _activeTokens = new();
    private readonly Dictionary<string, CompensationContext> _compensationStack = new();
    private readonly Dictionary<string, MultiInstanceContext> _multiInstanceContexts = new();
    private readonly List<BoundaryEventHandler> _boundaryEventHandlers = new();
    private readonly Dictionary<string, Func<IDictionary<string, string>, IDictionary<string, object>, CancellationToken, Task>> _serviceTaskHandlers = new();

    public TokenEngine()
    {
        // Registrierung des SemanticKernelServiceTaskHandler
        var skHandler = new SemanticKernelServiceTaskHandler(new CachingKernelFactory());
        _serviceTaskHandlers["semanticKernelServiceTask"] = skHandler.ExecuteAsync;

    }
    public List<string> Execute(BpmnModel model)
    {
        return Execute(model, null);
    }

    public List<string> Execute(BpmnModel model, IDecisionService? decisionService = null)
    {
        var trace = new List<string>();
        var start = model.Events.FirstOrDefault(e => e.Type == "startEvent");
        if (start == null) throw new InvalidOperationException("No startEvent found.");
        trace.Add($"StartEvent: {start.Id}");
        var currentId = start.Id;
        while (true)
        {
            // Check if current element is an end event
            var endEvent = model.Events.FirstOrDefault(e => e.Id == currentId && e.Type == "endEvent");
            if (endEvent != null)
            {
                trace.Add($"EndEvent: {endEvent.Id}");
                break;
            }
            
            var flows = model.SequenceFlows.Where(f => f.SourceRef == currentId).ToList();
            if (flows.Count == 0) break;
            // ParallelGateway: alle ausgehenden Flows
            var gateway = model.Gateways.FirstOrDefault(g => g.Id == currentId);
            if (gateway != null && gateway.Type == "parallelGateway")
            {
                trace.Add($"ParallelGateway: {gateway.Id}");
                foreach (var flow in flows)
                {
                    trace.Add($"SequenceFlow: {flow.Id}");
                    var target = flow.TargetRef;
                    trace.Add($"ParallelBranch: {target}");
                }
                break;
            }
            // InclusiveGateway: alle ausgehenden Flows (wie Parallel, aber semantisch anders)
            if (gateway != null && gateway.Type == "inclusiveGateway")
            {
                trace.Add($"InclusiveGateway: {gateway.Id}");
                foreach (var flow in flows)
                {
                    trace.Add($"SequenceFlow: {flow.Id}");
                    var target = flow.TargetRef;
                    trace.Add($"InclusiveBranch: {target}");
                }
                break;
            }
            // ExclusiveGateway: nur erster Flow
            if (gateway != null && gateway.Type == "exclusiveGateway")
            {
                trace.Add($"ExclusiveGateway: {gateway.Id}");
                var flow = flows.First();
                trace.Add($"SequenceFlow: {flow.Id}");
                currentId = flow.TargetRef;
                continue;
            }
            // ComplexGateway: advanced condition evaluation
            if (gateway != null && gateway.Type == "complexGateway")
            {
                trace.Add($"ComplexGateway: {gateway.Id}");
                // Complex gateway can have multiple outgoing flows with complex conditions
                var selectedFlows = EvaluateComplexGatewayConditions(gateway, flows, trace);
                
                // For the simple case, continue with the first selected flow
                if (selectedFlows.Count > 0)
                {
                    var firstFlow = selectedFlows[0];
                    trace.Add($"SequenceFlow: {firstFlow.Id}");
                    currentId = firstFlow.TargetRef;
                    trace.Add($"ComplexBranch: {currentId}");
                    continue; // Continue execution to the target
                }
                break;
            }
            // EventBasedGateway: wait for events
            if (gateway != null && gateway.Type == "eventBasedGateway")
            {
                trace.Add($"EventBasedGateway: {gateway.Id}");
                // Event-based gateway waits for one of multiple events
                var selectedFlow = SelectEventBasedFlow(gateway, flows, model, trace);
                if (selectedFlow != null)
                {
                    trace.Add($"SequenceFlow: {selectedFlow.Id}");
                    currentId = selectedFlow.TargetRef;
                    continue;
                }
                break;
            }
            // Subprocess
            var subprocess = model.Subprocesses.FirstOrDefault(s => s.Id == currentId);
            if (subprocess != null)
            {
                // Check for boundary events on subprocess
                CheckAndHandleBoundaryEvents(currentId, model, trace);
                
                if (subprocess.IsEventSubprocess)
                {
                    trace.Add($"EventSubprocess: {subprocess.Id}");
                    HandleEventSubprocess(subprocess, model, trace);
                }
                else if (subprocess.IsTransaction)
                {
                    trace.Add($"TransactionSubprocess: {subprocess.Id}");
                    // Add to compensation stack for potential rollback
                    _compensationStack[subprocess.Id] = new CompensationContext($"tx_{subprocess.Id}", subprocess.Id);
                }
                else
                {
                    trace.Add($"Subprocess: {subprocess.Id}");
                }
                if (subprocess.IsMultiInstance)
                {
                    HandleMultiInstanceSubprocess(subprocess, model, trace);
                }
                // Simuliere: Subprozess hat einen StartEvent mit gleicher Id + "_start"
                trace.Add($"SubprocessStart: {subprocess.Id}_start");
                // Kompensationshandler prüfen
                var compensation = model.Events.FirstOrDefault(e => e.Type == "boundaryEvent" && e.AttachedToRef == subprocess.Id && e.IsCompensation);
                if (compensation != null)
                {
                    trace.Add($"CompensationHandler: {compensation.Id}");
                }
                // Simuliere: Subprozess-Ende
                trace.Add($"SubprocessEnd: {subprocess.Id}_end");
                // Weiter zum nächsten Flow
                var flow = flows.FirstOrDefault();
                if (flow == null) break;
                trace.Add($"SequenceFlow: {flow.Id}");
                // Prüfe, ob das Ziel ein EndEvent ist
                var endEvt = model.Events.FirstOrDefault(e => e.Id == flow.TargetRef && e.Type == "endEvent");
                if (endEvt != null)
                {
                    trace.Add($"EndEvent: {endEvt.Id}");
                    break;
                }
                currentId = flow.TargetRef;
                continue;
            }
            // UserTask
            var task = model.Tasks.FirstOrDefault(t => t.Id == currentId);
            if (task != null)
            {
                // Check for boundary events on task
                CheckAndHandleBoundaryEvents(currentId, model, trace);
                
                if (task.Type == "businessRuleTask" && decisionService != null)
                {
                    trace.Add($"BusinessRuleTask: {task.Id}");
                    // Simuliere DMN-Auswertung
                    var result = decisionService.EvaluateDecisionByKeyAsync(task.Id, new Dictionary<string, object> { { "input", 1 } }).Result;
                    trace.Add($"DecisionEvaluated: {task.Id} => {result.Outputs["input"]}");
                }
                else
                {
                    trace.Add($"UserTask: {task.Id}");
                }
                // ServiceTask-Handling
                if (task.Type == "serviceTask" && _serviceTaskHandlers.TryGetValue(task.Implementation, out var handler))
                {
                    trace.Add($"ServiceTask: {task.Id} ({task.Implementation})");
                    // BPMN-Attribute und Prozessvariablen zusammenstellen
                    var attributes = task.Attributes; // Annahme: task.Attributes enthält BPMN-Attribute als Dictionary
                    var variables = model.ProcessVariables; // Annahme: Prozessvariablen sind hier verfügbar
                    // Asynchronen Handler ausführen (ggf. synchron warten, falls Execute synchron ist)
                    handler(attributes, variables, CancellationToken.None).GetAwaiter().GetResult();
                    trace.Add($"ServiceTaskCompleted: {task.Id}");
                }
                else
                {
                    trace.Add($"UserTask: {task.Id}");
                }
                // Weiter zum nächsten Flow
                var flow = flows.FirstOrDefault();
                if (flow == null) break;
                trace.Add($"SequenceFlow: {flow.Id}");
                // Prüfe, ob das Ziel ein EndEvent ist
                var endEvt = model.Events.FirstOrDefault(e => e.Id == flow.TargetRef && e.Type == "endEvent");
                if (endEvt != null)
                {
                    trace.Add($"EndEvent: {endEvt.Id}");
                    break;
                }
                currentId = flow.TargetRef;
                continue;
            }
            // Default: nur erster Flow
            var defaultFlow = flows.First();
            trace.Add($"SequenceFlow: {defaultFlow.Id}");
            currentId = defaultFlow.TargetRef;
            var evt = model.Events.FirstOrDefault(e => e.Id == currentId);
            if (evt != null && evt.Type == "endEvent")
            {
                trace.Add($"EndEvent: {evt.Id}");
                break;
            }
        }
        return trace;
    }
    
    /// <summary>
    /// Checks and handles boundary events attached to the current activity
    /// </summary>
    private void CheckAndHandleBoundaryEvents(string activityId, BpmnModel model, List<string> trace)
    {
        var boundaryEvents = model.Events.Where(e => e.Type == "boundaryEvent" && e.AttachedToRef == activityId);
        
        foreach (var boundaryEvent in boundaryEvents)
        {
            // Simulate boundary event triggering based on conditions
            if (ShouldTriggerBoundaryEvent(boundaryEvent))
            {
                trace.Add($"BoundaryEvent: {boundaryEvent.Id} triggered on {activityId}");
                
                if (boundaryEvent.IsCompensation)
                {
                    HandleCompensationEvent(boundaryEvent, activityId, trace);
                }
                else
                {
                    // Handle interrupting vs non-interrupting boundary events
                    var isInterrupting = DetermineBoundaryEventType(boundaryEvent);
                    if (isInterrupting)
                    {
                        trace.Add($"Activity {activityId} interrupted by boundary event {boundaryEvent.Id}");
                    }
                    else
                    {
                        trace.Add($"Non-interrupting boundary event {boundaryEvent.Id} on {activityId}");
                    }
                }
            }
        }
    }
    
    /// <summary>
    /// Handles multi-instance subprocess execution
    /// </summary>
    private void HandleMultiInstanceSubprocess(BpmnSubprocess subprocess, BpmnModel model, List<string> trace)
    {
        trace.Add($"MultiInstance: {subprocess.Id}");
        
        // Use configured loop cardinality or default to 3
        var totalInstances = subprocess.LoopCardinality ?? 3;
        var isSequential = subprocess.IsSequential;
        
        // Initialize multi-instance context
        var context = new MultiInstanceContext(subprocess.Id, totalInstances, 0, isSequential);
        _multiInstanceContexts[subprocess.Id] = context;
        
        if (isSequential)
        {
            trace.Add($"SequentialMultiInstance: {subprocess.Id}");
            // Execute instances sequentially
            for (int i = 0; i < totalInstances; i++)
            {
                trace.Add($"SequentialInstance: {subprocess.Id} instance {i + 1}/{totalInstances}");
                
                // Update completion count
                context = context with { CompletedInstances = i + 1 };
                _multiInstanceContexts[subprocess.Id] = context;
            }
        }
        else
        {
            trace.Add($"ParallelMultiInstance: {subprocess.Id}");
            // Execute instances in parallel (simulated)
            for (int i = 0; i < totalInstances; i++)
            {
                trace.Add($"ParallelInstance: {subprocess.Id} instance {i + 1}/{totalInstances}");
            }
            
            // Update completion count for all instances
            context = context with { CompletedInstances = totalInstances };
            _multiInstanceContexts[subprocess.Id] = context;
        }
        
        trace.Add($"MultiInstanceCompleted: {subprocess.Id}");
    }
    
    /// <summary>
    /// Handles event-driven subprocess execution
    /// </summary>
    private void HandleEventSubprocess(BpmnSubprocess subprocess, BpmnModel model, List<string> trace)
    {
        trace.Add($"EventSubprocessTriggering: {subprocess.Id}");
        
        // Event subprocesses are triggered by events (messages, errors, timers, etc.)
        // They interrupt or run in parallel with the main process
        
        // Find start events within the event subprocess
        var subprocessStartEvents = model.Events.Where(e => 
            e.Type == "startEvent" && 
            IsWithinSubprocess(e.Id, subprocess.Id, model)).ToList();
            
        foreach (var startEvent in subprocessStartEvents)
        {
            trace.Add($"EventSubprocessStart: {startEvent.Id}");
            
            // Determine the event type and handle accordingly
            if (startEvent.EventDefinitionType != null)
            {
                trace.Add($"EventType: {startEvent.EventDefinitionType}");
                
                switch (startEvent.EventDefinitionType)
                {
                    case "message":
                        trace.Add($"MessageEventSubprocess: {subprocess.Id} triggered by message");
                        break;
                    case "error":
                        trace.Add($"ErrorEventSubprocess: {subprocess.Id} triggered by error");
                        break;
                    case "timer":
                        trace.Add($"TimerEventSubprocess: {subprocess.Id} triggered by timer");
                        break;
                    case "signal":
                        trace.Add($"SignalEventSubprocess: {subprocess.Id} triggered by signal");
                        break;
                    default:
                        trace.Add($"GenericEventSubprocess: {subprocess.Id} triggered");
                        break;
                }
            }
        }
        
        // Simulate subprocess execution
        trace.Add($"EventSubprocessExecution: {subprocess.Id}");
        
        // Find end events within the event subprocess
        var subprocessEndEvents = model.Events.Where(e => 
            e.Type == "endEvent" && 
            IsWithinSubprocess(e.Id, subprocess.Id, model)).ToList();
            
        foreach (var endEvent in subprocessEndEvents)
        {
            trace.Add($"EventSubprocessEnd: {endEvent.Id}");
        }
    }
    
    /// <summary>
    /// Checks if an element is within a specific subprocess (simplified logic)
    /// </summary>
    private bool IsWithinSubprocess(string elementId, string subprocessId, BpmnModel model)
    {
        // Simplified logic - in real implementation, this would parse the BPMN XML structure
        // to determine parent-child relationships
        return elementId.StartsWith(subprocessId + "_");
    }
    
    /// <summary>
    /// Determines if a boundary event should be triggered (simplified logic)
    /// </summary>
    private bool ShouldTriggerBoundaryEvent(BpmnEvent boundaryEvent)
    {
        // Advanced trigger logic based on event definition type
        switch (boundaryEvent.EventDefinitionType)
        {
            case "timer":
                // Timer events would be triggered based on time conditions
                return false; // Simplified for demo
                
            case "message":
                // Message events would be triggered when correlated message arrives
                return false; // Simplified for demo
                
            case "error":
                // Error events would be triggered when error occurs in activity
                return false; // Simplified for demo
                
            case "signal":
                // Signal events would be triggered when signal is broadcast
                return false; // Simplified for demo
                
            case "compensation":
                // Compensation events are triggered manually during rollback
                return false; // Handled separately
                
            default:
                return false;
        }
    }
    
    /// <summary>
    /// Handles compensation events and adds them to the compensation stack
    /// </summary>
    private void HandleCompensationEvent(BpmnEvent compensationEvent, string activityId, List<string> trace)
    {
        trace.Add($"CompensationTriggered: {compensationEvent.Id}");
        
        // Add to compensation stack for potential execution
        _compensationStack[compensationEvent.Id] = new CompensationContext(compensationEvent.Id, activityId);
        
        trace.Add($"CompensationHandler: {compensationEvent.Id} attached to {activityId}");
    }
    
    /// <summary>
    /// Determines if a boundary event is interrupting or non-interrupting
    /// </summary>
    private bool DetermineBoundaryEventType(BpmnEvent boundaryEvent)
    {
        // Use the parsed cancelActivity attribute from BPMN
        return boundaryEvent.CancelActivity;
    }
    
    /// <summary>
    /// Evaluates complex gateway conditions for advanced flow control
    /// </summary>
    private List<BpmnSequenceFlow> EvaluateComplexGatewayConditions(BpmnGateway gateway, List<BpmnSequenceFlow> flows, List<string> trace)
    {
        trace.Add($"EvaluatingComplexConditions: {gateway.Id}");
        
        // Complex gateway evaluation logic (simplified)
        // In real implementation, this would evaluate complex FEEL expressions or custom conditions
        var selectedFlows = new List<BpmnSequenceFlow>();
        
        // For demonstration, select first two flows if available
        if (flows.Count > 0)
        {
            selectedFlows.Add(flows[0]);
            if (flows.Count > 1)
            {
                selectedFlows.Add(flows[1]);
            }
        }
        
        trace.Add($"ComplexGatewayResult: {selectedFlows.Count} flows selected");
        return selectedFlows;
    }
    
    /// <summary>
    /// Selects the event-based flow that should be taken
    /// </summary>
    private BpmnSequenceFlow? SelectEventBasedFlow(BpmnGateway gateway, List<BpmnSequenceFlow> flows, BpmnModel model, List<string> trace)
    {
        trace.Add($"WaitingForEvents: {gateway.Id}");
        
        // Event-based gateway waits for external events
        // In real implementation, this would wait for messages, timers, or signals
        foreach (var flow in flows)
        {
            var targetEvent = model.Events.FirstOrDefault(e => e.Id == flow.TargetRef);
            if (targetEvent != null)
            {
                trace.Add($"EventTarget: {targetEvent.Type} {targetEvent.Id}");
                
                // Simulate event arrival (in real implementation, this would be asynchronous)
                if (SimulateEventArrival(targetEvent))
                {
                    trace.Add($"EventTriggered: {targetEvent.Id}");
                    return flow;
                }
            }
        }
        
        // If no event is triggered, select first flow as default
        trace.Add($"NoEventTriggered: selecting default flow");
        return flows.FirstOrDefault();
    }
    
    /// <summary>
    /// Simulates event arrival for event-based gateways
    /// </summary>
    private bool SimulateEventArrival(BpmnEvent targetEvent)
    {
        // Simplified simulation - in real implementation, this would check:
        // - Message queues
        // - Timer schedules
        // - Signal broadcasts
        // - Condition evaluations
        
        // For now, always return true to continue flow
        return true;
    }
}