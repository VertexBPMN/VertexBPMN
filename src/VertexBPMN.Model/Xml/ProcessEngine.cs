using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;
using VertexBPMN.Domain.Model.Bpmn.Common;
using VertexBPMN.Domain.Model.Bpmn.Event;
using VertexBPMN.Domain.Model.Bpmn.Infrastructure;
using VertexBPMN.Domain.Model.Bpmn.Process;
using Task = System.Threading.Tasks.Task;

namespace VertexBPMN.Domain.Model;

/// <summary>
/// Advanced BPMN 2.0 Token Engine with support for boundary events, multi-instance, compensation, and transactions.
/// Lightweight, purely local in-process engine implementing IProcessEngine.
/// Intended for development, unit tests, single-node demos.
/// - No worker registry / distribution
/// - No messaging layer
/// - Optional DMN evaluation if parsers provided
/// - In-memory model registry (BPMN/DMN/CMMN)
/// - Deterministic synchronous execution loop with async facade
/// </summary>
public class ProcessEngine
{
    private readonly ILogger<ProcessEngine> _logger;
    //private readonly Dictionary<string, List<ExecutionToken>> _activeTokens = new();
    //private readonly Dictionary<string, CompensationContext> _compensationStack = new();
    //private readonly Dictionary<string, MultiInstanceContext> _multiInstanceContexts = new();
    //private readonly List<BoundaryEventHandler> _boundaryEventHandlers = new();
    //private readonly IServiceTaskRegistry _serviceTaskRegistry;

    public ProcessEngine() : this(NullLogger<ProcessEngine>.Instance)
    {      
    }
    public ProcessEngine(ILogger<ProcessEngine> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        //_serviceTaskRegistry = serviceTaskRegistry ?? throw new ArgumentNullException(nameof(serviceTaskRegistry));
    }

    //public ProcessEngine(ILogger<ProcessEngine> logger, IServiceTaskRegistry serviceTaskRegistry)
    //{
    //    _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    //    //_serviceTaskRegistry = serviceTaskRegistry ?? throw new ArgumentNullException(nameof(serviceTaskRegistry));
    //}

    public Task<List<string>> ExecuteAsync(BpmnModel model, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = Execute(model);
        return Task.FromResult(result);
    }

    public List<string> Execute(BpmnModel model)
    {
        return Execute(model, null);
    }

    //public Task<List<string>> ExecuteCaseAsync(CaseModel model, CancellationToken cancellationToken = default)
    //{
    //    // TokenEngine doesn't support CMMN, so return informative trace
    //    var trace = new List<string>
    //    {
    //        "CaseExecutionNotSupported: TokenEngine does not support CMMN case execution",
    //        $"CaseId: {model.Id}",
    //        $"PlanItems: {model.PlanItems.Count}",
    //        "Recommendation: Use DistributedTokenEngine (IDistributedProcessEngine) for CMMN support"
    //    };
    //    return Task.FromResult(trace);
    //}

    public Task<List<string>> ExecuteProcessAsync(string processId, CancellationToken cancellationToken = default)
    {
        // TokenEngine doesn't support process registry, so return informative trace
        var trace = new List<string>
        {
            "ProcessRegistryNotSupported: TokenEngine does not support process registry",
            $"ProcessId: {processId}",
            "Recommendation: Use DistributedTokenEngine (IDistributedProcessEngine) for process registry support"
        };
        return Task.FromResult(trace);
    }

    public Task<bool> CanExecuteAsync(string nodeId, CancellationToken cancellationToken = default)
    {
        // TokenEngine always can execute (single-threaded, local execution)
        return Task.FromResult(true);
    }

    public Task RegisterBpmnModelAsync(string processId, string bpmnXml, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException(
            "TokenEngine does not support model registration. " +
            "Use DistributedTokenEngine (IDistributedProcessEngine) for model registry features.");
    }

    public Task RegisterCmmnModelAsync(string caseId, string cmmnXml)
    {
        throw new NotSupportedException(
            "TokenEngine does not support CMMN. " +
            "Use DistributedTokenEngine (IDistributedProcessEngine) for CMMN support.");
    }

    public Task RegisterDmnModelAsync(string decisionId, string dmnXml)
    {
        throw new NotSupportedException(
            "TokenEngine does not support DMN registration. " +
            "Use DistributedTokenEngine (IDistributedProcessEngine) for DMN support.");
    }

    //public Task<CaseModel> GetCmmnModelAsync(string caseId)
    //{
    //    throw new NotSupportedException(
    //        "TokenEngine does not support CMMN. " +
    //        "Use DistributedTokenEngine (IDistributedProcessEngine) for CMMN support.");
    //}

    //public Task<List<HistoricalCaseData>> GetHistoricalCaseDataAsync(string caseId)
    //{
    //    throw new NotSupportedException(
    //        "TokenEngine does not support historical data. " +
    //        "Use DistributedTokenEngine (IDistributedProcessEngine) for historical data features.");
    //}

    public List<string> Execute(BpmnModel model, object decisionService = null)
    {
        var trace = new List<string>();
        
        ArgumentNullException.ThrowIfNull(model);
        
        var start = model.Events.FirstOrDefault(e => e is StartEvent);
        if (start == null) 
        {
            throw new InvalidOperationException("No startEvent found in BPMN model.");
        }
        
        trace.Add($"StartEvent: {start.Id}");
        var currentId = start.Id;
        
        // ✅ Safety: Prevent infinite loops with visited nodes tracking
        var visitedNodes = new HashSet<string>();
        var maxIterations = 1000; // Safety limit
        var iterations = 0;
        
        while (iterations < maxIterations)
        {
            iterations++;
            
            // Check if current element is an end event
            var endEvent = model.Events.FirstOrDefault(e => e.Id == currentId && e is EndEvent);
            if (endEvent != null)
            {
                trace.Add($"EndEvent: {endEvent.Id}");
                break;
            }
            
            var flows = model.SequenceFlows.Where(f => f.Id == currentId).ToList();
            if (flows.Count == 0) 
            {
                trace.Add($"NoOutgoingFlows: {currentId}");
                break;
            }

            // Gateway handling
            var gateway = model.Gateways.FirstOrDefault(g => g.Id == currentId);
            if (gateway != null)
            {
                var nextId = HandleGateway(gateway, flows, model, trace);
                if (nextId == null) break;
                currentId = nextId;
                continue;
            }

            // Subprocess handling
            var subprocess = model.Subprocesses.FirstOrDefault(s => s.Id == currentId);
            if (subprocess != null)
            {
                var nextId = HandleSubprocess(subprocess, flows, model, trace);
                if (nextId == null) break;
                currentId = nextId;
                continue;
            }

            // Task handling
            var task = model.Tasks.FirstOrDefault(t => t.Id == currentId);
            if (task != null)
            {
                var nextId =  HandleTaskAsync(task, flows, model, trace, decisionService);
                if (nextId == null) break;
                currentId = nextId.Result;
                continue;
            }

            // Default: follow first flow
            var defaultFlow = flows.FirstOrDefault();
            if (defaultFlow == null) break;
            
            trace.Add($"SequenceFlow: {defaultFlow.Id}");
            currentId = defaultFlow.Id;
            
            var evt = model.Events.FirstOrDefault(e => e.Id == currentId);
            if (evt is EndEvent)
            {
                trace.Add($"EndEvent: {evt.Id}");
                break;
            }
        }
        
        if (iterations >= maxIterations)
        {
            trace.Add("ExecutionLimitReached: Process execution stopped to prevent infinite loop");
            _logger.LogWarning("Process execution stopped after {MaxIterations} iterations to prevent infinite loop", maxIterations);
        }
        
        return trace;
    }

    /// <summary>
    /// Handles gateway execution and returns next node ID.
    /// </summary>
    private string? HandleGateway(Gateway gateway, List<SequenceFlow> flows, BpmnModel model, List<string> trace)
    {
        return gateway.Name switch
        {
            "parallelGateway" => HandleParallelGateway(gateway, flows, trace),
            "inclusiveGateway" => HandleInclusiveGateway(gateway, flows, trace),
            "exclusiveGateway" => HandleExclusiveGateway(gateway, flows, trace),
            "complexGateway" => HandleComplexGateway(gateway, flows, model, trace),
            "eventBasedGateway" => HandleEventBasedGateway(gateway, flows, model, trace),
            _ => HandleDefaultGateway(gateway, flows, trace)
        };
    }

    private string? HandleParallelGateway(Gateway gateway, List<SequenceFlow> flows, List<string> trace)
    {
        trace.Add($"ParallelGateway: {gateway.Id}");
        foreach (var flow in flows)
        {
            trace.Add($"SequenceFlow: {flow.Id}");
            trace.Add($"ParallelBranch: {flow.TargetRef}");
        }
        return null; // Parallel execution ends here in this simple implementation
    }

    private string? HandleInclusiveGateway(Gateway gateway, List<SequenceFlow> flows, List<string> trace)
    {
        trace.Add($"InclusiveGateway: {gateway.Id}");
        foreach (var flow in flows)
        {
            trace.Add($"SequenceFlow: {flow.Id}");
            trace.Add($"InclusiveBranch: {flow.TargetRef}");
        }
        return null; // Inclusive execution ends here in this simple implementation
    }

    private string? HandleExclusiveGateway(Gateway gateway, List<SequenceFlow> flows, List<string> trace)
    {
        trace.Add($"ExclusiveGateway: {gateway.Id}");
        var flow = flows.FirstOrDefault();
        if (flow == null) return null;
        
        trace.Add($"SequenceFlow: {flow.Id}");
        return flow.TargetRef.Name;
    }

    private string? HandleComplexGateway(Gateway gateway, List<SequenceFlow> flows, BpmnModel model, List<string> trace)
    {
        trace.Add($"ComplexGateway: {gateway.Id}");
        var selectedFlows = EvaluateComplexGatewayConditions(gateway, flows, trace);
        
        if (selectedFlows.Count > 0)
        {
            var firstFlow = selectedFlows[0];
            trace.Add($"SequenceFlow: {firstFlow.Id}");
            trace.Add($"ComplexBranch: {firstFlow.TargetRef}");
            return firstFlow.TargetRef.Name;
        }
        return null;
    }

    private string? HandleEventBasedGateway(Gateway gateway, List<SequenceFlow> flows, BpmnModel model, List<string> trace)
    {
        trace.Add($"EventBasedGateway: {gateway.Id}");
        var selectedFlow = SelectEventBasedFlow(gateway, flows, model, trace);
        if (selectedFlow != null)
        {
            trace.Add($"SequenceFlow: {selectedFlow.Id}");
            return selectedFlow.TargetRef.Name;
        }
        return null;
    }

    private string? HandleDefaultGateway(Gateway gateway, List<SequenceFlow> flows, List<string> trace)
    {
        trace.Add($"UnknownGateway: {gateway.Id} ({gateway.Type})");
        var flow = flows.FirstOrDefault();
        if (flow == null) return null;
        
        trace.Add($"SequenceFlow: {flow.Id}");
        return flow.TargetRef.Name;
    }

    /// <summary>
    /// Handles subprocess execution and returns next node ID.
    /// </summary>
    private string? HandleSubprocess(SubProcess subprocess, List<SequenceFlow> flows, BpmnModel model, List<string> trace)
    {
        // Check for boundary events on subprocess
        CheckAndHandleBoundaryEvents(subprocess.Id, model, trace);
        
        if (subprocess.IsForCompensation)
        {
            trace.Add($"EventSubprocess: {subprocess.Id}");
            HandleEventSubprocess(subprocess, model, trace);
        }
        else if (subprocess.IsTransaction)
        {
            trace.Add($"TransactionSubprocess: {subprocess.Id}");
            //HandleCompensationEvent(subprocess, subprocess.Id, trace);
            //_compensationStack[subprocess.Id] = new CompensationContext($"tx_{subprocess.Id}", subprocess.Id);
        }
        else
        {
            trace.Add($"Subprocess: {subprocess.Id}");
        }
        
        if (subprocess.IsMultiInstance)
        {
            HandleMultiInstanceSubprocess(subprocess, model, trace);
        }
        
        // Simulate subprocess execution
        trace.Add($"SubprocessStart: {subprocess.Id}_start");

        // Check for compensation handlers
        var compensation = model.Events.FirstOrDefault(e =>
            e.Type == "boundaryEvent");
            //&& 
            //e.AttachedToRef == subprocess.Id && 
            //e.IsCompensation);
        if (compensation != null)
        {
            trace.Add($"CompensationHandler: {compensation.Id}");
        }
        
        trace.Add($"SubprocessEnd: {subprocess.Id}_end");
        
        // Continue to next flow
        var flow = flows.FirstOrDefault();
        if (flow == null) return null;
        
        trace.Add($"SequenceFlow: {flow.Id}");
        
        // Check if target is end event
        var endEvent = model.Events.FirstOrDefault(e => e.Id == flow.TargetRef.Id && e.Type == "endEvent");
        if (endEvent != null)
        {
            trace.Add($"EndEvent: {endEvent.Id}");
            return null; // End execution
        }
        
        return flow.TargetRef.Name;
    }

    /// <summary>
    /// Handles task execution and returns next node ID.
    /// </summary>
    private async Task<string?> HandleTaskAsync(VertexBPMN.Domain.Model.Bpmn.Process.Task task, List<SequenceFlow> flows, BpmnModel model, List<string> trace, object? decisionService)
    {
        // Check for boundary events on task
        CheckAndHandleBoundaryEvents(task.Id, model, trace);
        
        switch (task.Name)
        {
            case "businessRuleTask":
                await HandleBusinessRuleTaskAsync(task, model, trace, decisionService);
                break;
                
            case "scriptTask":
                await HandleScriptTaskAsync(task, model, trace);
                break;
                
            case "serviceTask":
                await HandleServiceTaskAsync(task, model, trace);
                break;
                
            case "userTask":
                return HandleUserTask(task, trace); // User tasks pause execution
                
            default:
                trace.Add($"Task: {task.Id} ({task.Name})");
                break;
        }
        
        // Continue to next flow
        var flow = flows.FirstOrDefault();
        if (flow == null) return null;
        
        trace.Add($"SequenceFlow: {flow.Id}");
        
        // Check if target is end event
        var endEvent = model.Events.FirstOrDefault(e => e.Id == flow.TargetRef.Id && e.Type == "endEvent");
        if (endEvent != null)
        {
            trace.Add($"EndEvent: {endEvent.Id}");
            return null; // End execution
        }
        
        return flow.TargetRef.Name;
    }

    private async Task HandleBusinessRuleTaskAsync(VertexBPMN.Domain.Model.Bpmn.Process.Task task, BpmnModel model, List<string> trace, object? decisionService)
    {
        trace.Add($"BusinessRuleTask: {task.Id}");
        
        //if (decisionService != null)
        //{
        //    try
        //    {
        //        var result = await decisionService.EvaluateDecisionByKeyAsync(
        //            task.Id, 
        //            new Dictionary<string, object> { { "input", 1 } });
        //        trace.Add($"DecisionEvaluated: {task.Id} => {result.Variables.FirstOrDefault().Value}");
        //    }
        //    catch (Exception ex)
        //    {
        //        trace.Add($"DecisionError: {task.Id} => {ex.Message}");
        //        _logger.LogError(ex, "Error evaluating decision for task {TaskId}", task.Id);
        //    }
        //}
        //else
        //{
        //    trace.Add($"DecisionServiceNotAvailable: {task.Id}");
        //}
    }

    private async Task HandleScriptTaskAsync(VertexBPMN.Domain.Model.Bpmn.Process.Task task, BpmnModel model, List<string> trace)
    {
        trace.Add($"ScriptTask: {task.Id}");
        
        //try
        //{
        //    var variables = model.ProcessVariables ?? new Dictionary<string, object>();
        //    var handled = await ScriptTaskExecution.TryHandleScriptTaskAsync(task, variables, CancellationToken.None);
            
        //    if (handled)
        //    {
        //        trace.Add($"ScriptTaskCompleted: {task.Id}");
        //    }
        //    else
        //    {
        //        trace.Add($"ScriptTaskNotHandled: {task.Id}");
        //    }
        //}
        //catch (Exception ex)
        //{
        //    trace.Add($"ScriptTaskError: {task.Id} => {ex.GetType().Name}: {ex.Message}");
        //    _logger.LogError(ex, "Error executing script task {TaskId}", task.Id);
        //}
    }

    private async Task HandleServiceTaskAsync(VertexBPMN.Domain.Model.Bpmn.Process.Task task, BpmnModel model, List<string> trace)
    {
        trace.Add($"ServiceTask: {task.Id} ({task.Implementation})");
        
        var implementation = task.Implementation;
        if (string.IsNullOrEmpty(implementation))
        {
            trace.Add($"ServiceTaskNoImplementation: {task.Id}");
            return;
        }
        
        //try
        //{
        //    if (_serviceTaskRegistry.TryResolve(implementation, out var handler))
        //    {
        //        await handler.ExecuteAsync(task.Attributes, model.ProcessVariables ?? new Dictionary<string, object>(), CancellationToken.None);
        //        trace.Add($"ServiceTaskCompleted: {task.Id}");
        //    }
        //    else
        //    {
        //        trace.Add($"ServiceTaskHandlerNotFound: {implementation}");
        //        _logger.LogWarning("Service task handler not found for implementation: {Implementation}", implementation);
        //        await HandleUnregisteredServiceTaskAsync(task, model.ProcessVariables, trace);
        //    }
        //}
        //catch (Exception ex)
        //{
        //    trace.Add($"ServiceTaskError: {task.Id} => {ex.GetType().Name}: {ex.Message}");
        //    _logger.LogError(ex, "Error executing service task {TaskId}", task.Id);
        //}
        
        //// Handle Zeebe-specific attributes
        //await HandleZeebeAttributesAsync(task, model, trace);
    }

    private string? HandleUserTask(VertexBPMN.Domain.Model.Bpmn.Process.Task task, List<string> trace)
    {
        trace.Add($"UserTask: {task.Id} ({task.Name})");
        
        // Log task attributes
        if (task.Attributes != null)
        {
            foreach (var attribute in task.Attributes)
            {
                trace.Add($"Attribute: {attribute.Key} = {attribute.Value}");
            }
        }
        
        // Create user task
        var userTaskId = Guid.NewGuid().ToString();
        var userTaskDetails = new
        {
            TaskId = task.Id,
            Name = task.Name,
            Attributes = task.Attributes,
            AssignedTo = task.Attributes?.ContainsKey("assignee") == true ? task.Attributes["assignee"] : "Unassigned",
            DueDate = task.Attributes?.ContainsKey("dueDate") == true ? task.Attributes["dueDate"] : null
        };
        
        trace.Add($"UserTaskCreated: {userTaskId} for task {task.Id}");
        _ = PersistUserTaskAsync(userTaskId, userTaskDetails);
        trace.Add($"UserTaskPaused: Waiting for completion of task {task.Id}");
        
        return null; // Pause execution for user tasks
    }

    //private async Task HandleZeebeAttributesAsync(VertexBPMN.Domain.Model.Bpmn.Process.Task task, BpmnModel model, List<string> trace)
    //{
    //    if (task.Attributes == null) return;
        
    //    // Handle Zeebe task definition
    //    if (task.Attributes.TryGetValue("zeebe:taskDefinition", out var taskDefinitionType) && 
    //        !string.IsNullOrWhiteSpace(taskDefinitionType))
    //    {
    //        trace.Add($"ZeebeTaskDefinition: {taskDefinitionType}");
            
    //        try
    //        {
    //            if (_serviceTaskRegistry.TryResolve(taskDefinitionType, out var handler))
    //            {
    //                await handler.ExecuteAsync(task.Attributes, model.ProcessVariables ?? new Dictionary<string, object>(), CancellationToken.None);
    //                trace.Add($"ZeebeTaskCompleted: {task.Id}");
    //            }
    //        }
    //        catch (Exception ex)
    //        {
    //            trace.Add($"ZeebeTaskError: {task.Id} => {ex.GetType().Name}: {ex.Message}");
    //            _logger.LogError(ex, "Error executing Zeebe task {TaskId}", task.Id);
    //        }
    //    }
        
    //    // Handle Zeebe I/O mapping
    //    if (task.Attributes.TryGetValue("zeebe:ioMapping", out var ioMappingJson))
    //    {
    //        try
    //        {
    //            var ioMapping = JsonSerializer.Deserialize<Dictionary<string, string>>(ioMappingJson);
    //            if (ioMapping != null)
    //            {
    //                if (model.ProcessVariables == null)
    //                    model = model with { ProcessVariables = new Dictionary<string, object>() };
    //                foreach (var (target, source) in ioMapping)
    //                {
    //                    if (source.StartsWith("="))
    //                    {
    //                        model.ProcessVariables[target] = EvaluateExpression(source, model.ProcessVariables);
    //                    }
    //                    else
    //                    {
    //                        model.ProcessVariables[target] = source;
    //                    }
    //                }
                    
    //                trace.Add($"ZeebeIOMappingApplied: {task.Id}");
    //            }
    //        }
    //        catch (Exception ex)
    //        {
    //            trace.Add($"ZeebeIOMappingError: {task.Id} => {ex.Message}");
    //            _logger.LogError(ex, "Error processing Zeebe I/O mapping for task {TaskId}", task.Id);
    //        }
    //    }
    //}

    private async Task PersistUserTaskAsync(string userTaskId, object userTaskDetails)
    {
        _logger.LogInformation("Persisting user task: {UserTaskId} with details: {UserTaskDetails}", 
            userTaskId, userTaskDetails);
        await Task.CompletedTask;
    }

    public async Task CompleteUserTaskAsync(string userTaskId, IDictionary<string, object> processVariables)
    {
        _logger.LogInformation("Completing user task: {UserTaskId}", userTaskId);
        await Task.CompletedTask;
    }

    private object? EvaluateExpression(string expression, IDictionary<string, object> variables)
    {
        var variableName = expression.TrimStart('=');
        return variables.TryGetValue(variableName, out var value) ? value : null;
    }

    private async Task HandleUnregisteredServiceTaskAsync(VertexBPMN.Domain.Model.Bpmn.Process.Task task, IDictionary<string, object>? processVariables, List<string> trace)
    {
        trace.Add($"ExecutingDefaultHandler: {task.Id}");
        
        if (task.Attributes != null)
        {
            foreach (var attribute in task.Attributes)
            {
                trace.Add($"Attribute: {attribute.Key} = {attribute.Value}");
            }
        }
        
        var resultVariable = task.Attributes?.ContainsKey("resultVariable") == true
            ? task.Attributes["resultVariable"]
            : $"{task.Id}_Result";
        
        var result = $"Default result for task {task.Id}";
        processVariables ??= new Dictionary<string, object>();
        processVariables[resultVariable] = result;
        
        trace.Add($"DefaultResultSet: {task.Id} => {result}");
        await Task.CompletedTask;
    }

    /// <summary>
    /// Checks and handles boundary events attached to the current activity
    /// </summary>
    private void CheckAndHandleBoundaryEvents(string activityId, BpmnModel model, List<string> trace)
    {
        var boundaryEvents = model.Events.Where(e => e is BoundaryEvent b && b.AttachedToRef.Id == activityId);
        
        foreach (BoundaryEvent boundaryEvent in boundaryEvents)
        {
            if (ShouldTriggerBoundaryEvent(boundaryEvent))
            {
                trace.Add($"BoundaryEvent: {boundaryEvent.Id} triggered on {activityId}");
                
                if (boundaryEvent != null)
                {
                    HandleCompensationEvent(boundaryEvent, activityId, trace);
                }
                else
                {
                    var isInterrupting = DetermineBoundaryEventType(boundaryEvent);
                    if (isInterrupting)
                    {
                        trace.Add($"ActivityInterrupted: {activityId} by {boundaryEvent.Id}");
                    }
                    else
                    {
                        trace.Add($"NonInterruptingBoundaryEvent: {boundaryEvent.Id} on {activityId}");
                    }
                }
            }
        }
    }
    
    /// <summary>
    /// Handles multi-instance subprocess execution
    /// </summary>
    private void HandleMultiInstanceSubprocess(SubProcess subprocess, BpmnModel model, List<string> trace)
    {
        trace.Add($"MultiInstance: {subprocess.Id}");
        
        var totalInstances = subprocess.LoopCardinality >= 1 ? subprocess.LoopCardinality : 3;
        var isSequential = subprocess.IsForCompensation;

        //var context = new MultiInstanceContext(subprocess.Id, totalInstances, 0, isSequential);
        //_multiInstanceContexts[subprocess.Id] = context;
        
        //if (isSequential)
        //{
        //    trace.Add($"SequentialMultiInstance: {subprocess.Id}");
        //    for (int i = 0; i < totalInstances; i++)
        //    {
        //        trace.Add($"SequentialInstance: {subprocess.Id} instance {i + 1}/{totalInstances}");
        //        context = context with { CompletedInstances = i + 1 };
        //        _multiInstanceContexts[subprocess.Id] = context;
        //    }
        //}
        //else
        //{
        //    trace.Add($"ParallelMultiInstance: {subprocess.Id}");
        //    for (int i = 0; i < totalInstances; i++)
        //    {
        //        trace.Add($"ParallelInstance: {subprocess.Id} instance {i + 1}/{totalInstances}");
        //    }
        //    context = context with { CompletedInstances = totalInstances };
        //    _multiInstanceContexts[subprocess.Id] = context;
        //}
        
        trace.Add($"MultiInstanceCompleted: {subprocess.Id}");
    }
    
    /// <summary>
    /// Handles event-driven subprocess execution
    /// </summary>
    private void HandleEventSubprocess(SubProcess subprocess, BpmnModel model, List<string> trace)
    {
        trace.Add($"EventSubprocessTriggering: {subprocess.Id}");
        
        var subprocessStartEvents = model.Events.Where(e => 
            e.Type == "startEvent" && 
            IsWithinSubprocess(e.Id, subprocess.Id, model)).ToList();
            
        foreach (var startEvent in subprocessStartEvents)
        {
            trace.Add($"EventSubprocessStart: {startEvent.Id}");
            
            if (startEvent.EventDefinitions.Any())
            {
                trace.Add($"EventType: {startEvent.EventDefinitions.FirstOrDefault()}");
                
                switch (startEvent.Type)
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
        
        trace.Add($"EventSubprocessExecution: {subprocess.Id}");
        
        var subprocessEndEvents = model.Events.Where(e => 
            e.Type == "endEvent" && 
            IsWithinSubprocess(e.Id, subprocess.Id, model)).ToList();
            
        foreach (var endEvent in subprocessEndEvents)
        {
            trace.Add($"EventSubprocessEnd: {endEvent.Id}");
        }
    }
    
    private bool IsWithinSubprocess(string elementId, string subprocessId, BpmnModel model)
    {
        return elementId.StartsWith(subprocessId + "_");
    }
    
    private bool ShouldTriggerBoundaryEvent(BoundaryEvent boundaryEvent)
    {
        return boundaryEvent.EventDefinitions.First().Id switch
        {
            "timer" => false, // Simplified for demo
            "message" => false, // Simplified for demo
            "error" => false, // Simplified for demo
            "signal" => false, // Simplified for demo
            "compensation" => false, // Handled separately
            _ => false
        };
    }
    
    private void HandleCompensationEvent(Event compensationEvent, string activityId, List<string> trace)
    {
        trace.Add($"CompensationTriggered: {compensationEvent.Id}");
        //_compensationStack[compensationEvent.Id] = new CompensationContext(compensationEvent.Id, activityId);
        trace.Add($"CompensationHandler: {compensationEvent.Id} attached to {activityId}");
    }
    
    private bool DetermineBoundaryEventType(BoundaryEvent boundaryEvent)
    {
        return boundaryEvent.CancelActivity;
    }
    
    private List<SequenceFlow> EvaluateComplexGatewayConditions(Gateway gateway, List<SequenceFlow> flows, List<string> trace)
    {
        trace.Add($"EvaluatingComplexConditions: {gateway.Id}");
        
        var selectedFlows = new List<SequenceFlow>();
        
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
    
    private SequenceFlow? SelectEventBasedFlow(Gateway gateway, List<SequenceFlow> flows, BpmnModel model, List<string> trace)
    {
        trace.Add($"WaitingForEvents: {gateway.Id}");
        
        foreach (var flow in flows)
        {
            var targetEvent = model.Events.FirstOrDefault(e => e.Id == flow.TargetRef.Id);
            if (targetEvent != null)
            {
                trace.Add($"EventTarget: {targetEvent.Type} {targetEvent.Id}");
                
                if (SimulateEventArrival(targetEvent))
                {
                    trace.Add($"EventTriggered: {targetEvent.Id}");
                    return flow;
                }
            }
        }
        
        trace.Add($"NoEventTriggered: selecting default flow");
        return flows.FirstOrDefault();
    }
    
    private bool SimulateEventArrival(Event targetEvent)
    {
        if (targetEvent == null)
        {
            _logger.LogWarning("Target event is null. Skipping event arrival simulation.");
            return false;
        }

        _logger.LogInformation("Simulating event arrival for event: {EventId} of type: {EventType}", 
            targetEvent.Id, targetEvent.Type);

        return targetEvent.Type switch
        {
            "message" => SimulateMessageEvent(targetEvent),
            "timer" => SimulateTimerEvent(targetEvent),
            "signal" => SimulateSignalEvent(targetEvent),
            "error" => SimulateErrorEvent(targetEvent),
            "condition" => SimulateConditionEvent(targetEvent),
            _ => false
        };
    }
    
    private bool SimulateMessageEvent(Event targetEvent)
    {
        _logger.LogInformation("Simulating message event for event: {EventId}", targetEvent.Id);

        var definition = targetEvent.EventDefinitions.FirstOrDefault(d => d is MessageEventDefinition) as MessageEventDefinition;

        if (definition?.CorrelationKey != null && definition.CorrelationKey == "expectedCorrelationKey")
        {
            _logger.LogInformation("Message event {EventId} triggered with correlation key: {CorrelationKey}", 
                targetEvent.Id, definition.CorrelationKey);
            return true;
        }

        _logger.LogWarning("Message event {EventId} not triggered. Correlation key mismatch.", targetEvent.Id);
        return false;
    }
    
    private bool SimulateTimerEvent(Event targetEvent)
    {
        _logger.LogInformation("Simulating timer event for event: {EventId}", targetEvent.Id);
        
        var timerDueDate = DateTime.UtcNow.AddSeconds(-10);
        if (DateTime.UtcNow >= timerDueDate)
        {
            _logger.LogInformation("Timer event {EventId} triggered. Timer is due.", targetEvent.Id);
            return true;
        }

        _logger.LogWarning("Timer event {EventId} not triggered. Timer is not yet due.", targetEvent.Id);
        return false;
    }
    
    private bool SimulateSignalEvent(Event targetEvent)
    {
        _logger.LogInformation("Simulating signal event for event: {EventId}", targetEvent.Id);
        var definition = targetEvent.EventDefinitions.FirstOrDefault(d => d is SignalEventDefinition) as SignalEventDefinition;

        if (definition.SignalRef.Id ==  "expectedSignal") 
        {
            _logger.LogInformation("Signal event {EventId} triggered with signal name: {SignalName}", 
                targetEvent.Id, definition.SignalRef);
            return true;
        }

        _logger.LogWarning("Signal event {EventId} not triggered. Signal name mismatch.", targetEvent.Id);
        return false;
    }
    
    private bool SimulateErrorEvent(Event targetEvent)
    {
        _logger.LogInformation("Simulating error event for event: {EventId}", targetEvent.Id);
        var definition = targetEvent.EventDefinitions.FirstOrDefault(d => d is ErrorEventDefinition) as ErrorEventDefinition;

        if (definition.ErrorRef?.ErrorCode == "expectedErrorCode")
        {
            _logger.LogInformation("Error event {EventId} triggered with error code: {ErrorCode}", 
                targetEvent.Id, definition.ErrorRef?.ErrorCode);
            return true;
        }

        _logger.LogWarning("Error event {EventId} not triggered. Error code mismatch.", targetEvent.Id);
        return false;
    }
    
    private bool SimulateConditionEvent(Event targetEvent)
    {
        _logger.LogInformation("Simulating condition event for event: {EventId}", targetEvent.Id);
        var definition = targetEvent.EventDefinitions.FirstOrDefault(d => d is ConditionalEventDefinition) as ConditionalEventDefinition;

        if (definition.Condition.Id ==  "expectedCondition")
        {
            _logger.LogInformation("Condition event {EventId} triggered. Condition is met.", targetEvent.Id);
            return true;
        }

        _logger.LogWarning("Condition event {EventId} not triggered. Condition not met.", targetEvent.Id);
        return false;
    }
}