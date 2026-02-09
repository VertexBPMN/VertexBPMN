using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Domain.Model.Bpmn;
using VertexBPMN.Domain.Model.Cmn;
using VertexBPMN.Infrastructure.Scripting;

namespace VertexBPMN.Engine;

public class ProcessEngine : IProcessEngine
{
    private readonly ILogger<ProcessEngine> _logger;
    private readonly Dictionary<string, List<ExecutionToken>> _activeTokens = new();
    private readonly Dictionary<string, CompensationContext> _compensationStack = new();
    private readonly Dictionary<string, MultiInstanceContext> _multiInstanceContexts = new();
    private readonly List<BoundaryEventHandler> _boundaryEventHandlers = new();
    private readonly IServiceTaskRegistry _serviceTaskRegistry;
    private readonly IDecisionService? _decisionService; // optional injected DMN service

    public ProcessEngine() : this(NullLogger<ProcessEngine>.Instance, NullServiceTaskRegistry.Instance)
    {
    }

    public ProcessEngine(ILogger<ProcessEngine> logger, IServiceTaskRegistry serviceTaskRegistry,
        IDecisionService? decisionService = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _serviceTaskRegistry = serviceTaskRegistry ?? throw new ArgumentNullException(nameof(serviceTaskRegistry));
        _decisionService = decisionService; // may be null
    }

    public Task<List<string>> ExecuteAsync(BpmnModel model, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Execute(model, _decisionService, cancellationToken));
    }

    public List<string> Execute(BpmnModel model)
    {
        return Execute(model, _decisionService, CancellationToken.None);
    }

    // Backwards compatible signature
    public List<string> Execute(BpmnModel model, IDecisionService? decisionService = null)
    {
        return Execute(model, decisionService, CancellationToken.None);
    }

    public List<string> Execute(BpmnModel model, IDecisionService? decisionService, CancellationToken ct)
    {
        var trace = new List<string>();
        ArgumentNullException.ThrowIfNull(model);
        var start = model.Events.FirstOrDefault(e => e.Type == "startEvent") ??
                    throw new InvalidOperationException("No startEvent found in BPMN model.");
        trace.Add($"StartEvent: {start.Id}");
        var currentId = start.Id;
        var maxIterations = 1000;
        var iterations = 0;
        while (iterations < maxIterations)
        {
            ct.ThrowIfCancellationRequested();
            iterations++;
            var endEvent = model.Events.FirstOrDefault(e => e.Id == currentId && e.Type == "endEvent");
            if (endEvent != null)
            {
                trace.Add($"EndEvent: {endEvent.Id}");
                break;
            }

            var flows = model.SequenceFlows.Where(f => f.SourceRef == currentId).ToList();
            if (flows.Count == 0)
            {
                trace.Add($"NoOutgoingFlows: {currentId}");
                break;
            }

            var gateway = model.Gateways.FirstOrDefault(g => g.Id == currentId);
            if (gateway != null)
            {
                var nextId = HandleGateway(gateway, flows, model, trace);
                if (nextId == null) break;
                currentId = nextId;
                continue;
            }

            var subprocess = model.Subprocesses.FirstOrDefault(s => s.Id == currentId);
            if (subprocess != null)
            {
                var nextId = HandleSubprocess(subprocess, flows, model, trace);
                if (nextId == null) break;
                currentId = nextId;
                continue;
            }

            var task = model.Tasks.FirstOrDefault(t => t.Id == currentId);
            if (task != null)
            {
                var nextIdTask = HandleTaskAsync(task, flows, model, trace, decisionService ?? _decisionService, ct);
                if (nextIdTask == null) break;
                currentId = nextIdTask.Result;
                continue;
            }

            var defaultFlow = flows.FirstOrDefault();
            if (defaultFlow == null) break;
            trace.Add($"SequenceFlow: {defaultFlow.Id}");
            currentId = defaultFlow.TargetRef;
            var evt = model.Events.FirstOrDefault(e => e.Id == currentId);
            if (evt?.Type == "endEvent")
            {
                trace.Add($"EndEvent: {evt.Id}");
                break;
            }
        }

        if (iterations >= maxIterations)
        {
            trace.Add("ExecutionLimitReached: Process execution stopped to prevent infinite loop");
            _logger.LogWarning("Process execution stopped after {MaxIterations} iterations", maxIterations);
        }

        return trace;
    }

    private string? HandleGateway(BpmnGateway gateway, List<BpmnSequenceFlow> flows, BpmnModel model,
        List<string> trace)
    {
        return gateway.Type switch
        {
            "parallelGateway" => HandleParallelGateway(gateway, flows, trace),
            "inclusiveGateway" => HandleInclusiveGateway(gateway, flows, trace),
            "exclusiveGateway" => HandleExclusiveGateway(gateway, flows, trace),
            "complexGateway" => HandleComplexGateway(gateway, flows, model, trace),
            "eventBasedGateway" => HandleEventBasedGateway(gateway, flows, model, trace),
            _ => HandleDefaultGateway(gateway, flows, trace)
        };
    }

    private string? HandleParallelGateway(BpmnGateway gateway, List<BpmnSequenceFlow> flows, List<string> trace)
    {
        trace.Add($"ParallelGateway: {gateway.Id}");
        foreach (var flow in flows)
        {
            trace.Add($"SequenceFlow: {flow.Id}");
            trace.Add($"ParallelBranch: {flow.TargetRef}");
        }

        return null;
    }

    private string? HandleInclusiveGateway(BpmnGateway gateway, List<BpmnSequenceFlow> flows, List<string> trace)
    {
        trace.Add($"InclusiveGateway: {gateway.Id}");
        foreach (var flow in flows)
        {
            trace.Add($"SequenceFlow: {flow.Id}");
            trace.Add($"InclusiveBranch: {flow.TargetRef}");
        }

        return null;
    }

    private string? HandleExclusiveGateway(BpmnGateway gateway, List<BpmnSequenceFlow> flows, List<string> trace)
    {
        trace.Add($"ExclusiveGateway: {gateway.Id}");
        var flow = flows.FirstOrDefault();
        if (flow == null) return null;
        trace.Add($"SequenceFlow: {flow.Id}");
        return flow.TargetRef;
    }

    private string? HandleComplexGateway(BpmnGateway gateway, List<BpmnSequenceFlow> flows, BpmnModel model,
        List<string> trace)
    {
        trace.Add($"ComplexGateway: {gateway.Id}");
        var selectedFlows = EvaluateComplexGatewayConditions(gateway, flows, trace);
        if (selectedFlows.Count > 0)
        {
            var f = selectedFlows[0];
            trace.Add($"SequenceFlow: {f.Id}");
            trace.Add($"ComplexBranch: {f.TargetRef}");
            return f.TargetRef;
        }

        return null;
    }

    private string? HandleEventBasedGateway(BpmnGateway gateway, List<BpmnSequenceFlow> flows, BpmnModel model,
        List<string> trace)
    {
        trace.Add($"EventBasedGateway: {gateway.Id}");
        var selected = SelectEventBasedFlow(gateway, flows, model, trace);
        if (selected != null)
        {
            trace.Add($"SequenceFlow: {selected.Id}");
            return selected.TargetRef;
        }

        return null;
    }

    private string? HandleDefaultGateway(BpmnGateway gateway, List<BpmnSequenceFlow> flows, List<string> trace)
    {
        trace.Add($"UnknownGateway: {gateway.Id} ({gateway.Type})");
        var flow = flows.FirstOrDefault();
        if (flow == null) return null;
        trace.Add($"SequenceFlow: {flow.Id}");
        return flow.TargetRef;
    }

    private string? HandleSubprocess(BpmnSubprocess subprocess, List<BpmnSequenceFlow> flows, BpmnModel model,
        List<string> trace)
    {
        CheckAndHandleBoundaryEvents(subprocess.Id, model, trace);
        if (subprocess.IsEventSubprocess)
        {
            trace.Add($"EventSubprocess: {subprocess.Id}");
            HandleEventSubprocess(subprocess, model, trace);
        }
        else if (subprocess.IsTransaction)
        {
            trace.Add($"TransactionSubprocess: {subprocess.Id}");
            _compensationStack[subprocess.Id] = new CompensationContext($"tx_{subprocess.Id}", subprocess.Id);
        }
        else
        {
            trace.Add($"Subprocess: {subprocess.Id}");
        }

        if (IsMultiInstance(subprocess)) HandleMultiInstanceSubprocess(subprocess, model, trace);
        trace.Add($"SubprocessStart: {subprocess.Id}_start");
        var compensation = model.Events.FirstOrDefault(e =>
            e.Type == "boundaryEvent" && GetAttachedTo(e) == subprocess.Id && IsCompensationEvent(e));
        if (compensation != null) trace.Add($"CompensationHandler: {compensation.Id}");
        trace.Add($"SubprocessEnd: {subprocess.Id}_end");
        var flow = flows.FirstOrDefault();
        if (flow == null) return null;
        trace.Add($"SequenceFlow: {flow.Id}");
        var endEvent = model.Events.FirstOrDefault(e => e.Id == flow.TargetRef && e.Type == "endEvent");
        if (endEvent != null)
        {
            trace.Add($"EndEvent: {endEvent.Id}");
            return null;
        }

        return flow.TargetRef;
    }

    private async Task<string?> HandleTaskAsync(BpmnTask task, List<BpmnSequenceFlow> flows, BpmnModel model,
        List<string> trace, IDecisionService? decisionService, CancellationToken ct)
    {
        CheckAndHandleBoundaryEvents(task.Id, model, trace);
        switch (task.Type)
        {
            case "businessRuleTask": await HandleBusinessRuleTaskAsync(task, model, trace, decisionService, ct); break;
            case "scriptTask": await HandleScriptTaskAsync(task, model, trace, ct); break;
            case "serviceTask": await HandleServiceTaskAsync(task, model, trace, ct); break;
            case "userTask": return HandleUserTask(task, trace);
            default: trace.Add($"Task: {task.Id} ({task.Type})"); break;
        }

        var flow = flows.FirstOrDefault();
        if (flow == null) return null;
        trace.Add($"SequenceFlow: {flow.Id}");
        var endEvent = model.Events.FirstOrDefault(e => e.Id == flow.TargetRef && e.Type == "endEvent");
        if (endEvent != null)
        {
            trace.Add($"EndEvent: {endEvent.Id}");
            return null;
        }

        return flow.TargetRef;
    }

    private async Task HandleBusinessRuleTaskAsync(BpmnTask task, BpmnModel model, List<string> trace,
        IDecisionService? decisionService, CancellationToken ct)
    {
        trace.Add($"BusinessRuleTask: {task.Id}");
        if (decisionService == null)
        {
            trace.Add($"DecisionServiceNotAvailable: {task.Id}");
            return;
        }

        try
        {
            var variables = model.ProcessVariables ?? new Dictionary<string, object>();
            var decisionKey =
                task.Attributes != null && task.Attributes.TryGetValue("decisionRef", out var refKey) &&
                !string.IsNullOrWhiteSpace(refKey)
                    ? refKey
                    : task.Id;
            string? tenantId = null;
            if (task.Attributes != null && task.Attributes.TryGetValue("tenantId", out var tId) &&
                !string.IsNullOrWhiteSpace(tId)) tenantId = tId;
            var result = await decisionService.EvaluateDecisionByKeyAsync(decisionKey, variables, tenantId, ct);
            var mergeMode = task.Attributes != null && task.Attributes.TryGetValue("decisionMerge", out var mm)
                ? mm
                : "overwrite"; // overwrite | isolate
            if (result?.Variables != null)
            {
                if (string.Equals(mergeMode, "overwrite", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var kv in result.Variables) variables[kv.Key] = kv.Value;
                    trace.Add($"DecisionEvaluated: {decisionKey} VarsMerged: {result.Variables.Count}");
                }
                else
                {
                    // isolate: store under a scoped key without polluting root variable space
                    var scopeKey = $"decision:{decisionKey}";
                    variables[scopeKey] = new Dictionary<string, object>(result.Variables);
                    trace.Add($"DecisionEvaluated: {decisionKey} VarsIsolated: {result.Variables.Count}");
                }
            }
            else
            {
                trace.Add($"DecisionEvaluated: {decisionKey} (no vars)");
            }
        }
        catch (OperationCanceledException)
        {
            trace.Add($"DecisionCanceled: {task.Id}");
            throw;
        }
        catch (Exception ex)
        {
            trace.Add($"DecisionError: {task.Id} => {ex.Message}");
            _logger.LogError(ex, "Error evaluating decision for task {TaskId}", task.Id);
        }
    }

    private async Task HandleScriptTaskAsync(BpmnTask task, BpmnModel model, List<string> trace, CancellationToken ct)
    {
        trace.Add($"ScriptTask: {task.Id}");
        try
        {
            var variables = model.ProcessVariables ?? new Dictionary<string, object>();
            ct.ThrowIfCancellationRequested();
            var handled = await ScriptTaskExecution.TryHandleScriptTaskAsync(task, variables, ct);
            trace.Add(handled ? $"ScriptTaskCompleted: {task.Id}" : $"ScriptTaskNotHandled: {task.Id}");
        }
        catch (OperationCanceledException)
        {
            trace.Add($"ScriptTaskCanceled: {task.Id}");
            throw;
        }
        catch (Exception ex)
        {
            trace.Add($"ScriptTaskError: {task.Id} => {ex.GetType().Name}: {ex.Message}");
            _logger.LogError(ex, "Error executing script task {TaskId}", task.Id);
        }
    }

    private async Task HandleServiceTaskAsync(BpmnTask task, BpmnModel model, List<string> trace, CancellationToken ct)
    {
        trace.Add($"ServiceTask: {task.Id} ({task.Implementation})");
        var implementation = task.Implementation;
        if (string.IsNullOrEmpty(implementation))
        {
            trace.Add($"ServiceTaskNoImplementation: {task.Id}");
            return;
        }

        try
        {
            if (_serviceTaskRegistry.TryResolve(implementation, out var handler))
            {
                await handler.ExecuteAsync(task.Attributes, model.ProcessVariables ?? new Dictionary<string, object>(),
                    ct);
                trace.Add($"ServiceTaskCompleted: {task.Id}");
            }
            else
            {
                trace.Add($"ServiceTaskHandlerNotFound: {implementation}");
                _logger.LogWarning("Service task handler not found for implementation: {Implementation}",
                    implementation);
                await HandleUnregisteredServiceTaskAsync(task, model.ProcessVariables, trace);
            }
        }
        catch (OperationCanceledException)
        {
            trace.Add($"ServiceTaskCanceled: {task.Id}");
            throw;
        }
        catch (Exception ex)
        {
            trace.Add($"ServiceTaskError: {task.Id} => {ex.GetType().Name}: {ex.Message}");
            _logger.LogError(ex, "Error executing service task {TaskId}", task.Id);
        }

        await HandleZeebeAttributesAsync(task, model, trace);
    }

    private string? HandleUserTask(BpmnTask task, List<string> trace)
    {
        trace.Add($"UserTask: {task.Id} ({task.Name})");
        if (task.Attributes != null)
            foreach (var attribute in task.Attributes)
                trace.Add($"Attribute: {attribute.Key} = {attribute.Value}");
        var userTaskId = Guid.NewGuid().ToString();
        var userTaskDetails = new
        {
            TaskId = task.Id, task.Name, task.Attributes,
            AssignedTo = task.Attributes?.ContainsKey("assignee") == true ? task.Attributes["assignee"] : "Unassigned",
            DueDate = task.Attributes?.ContainsKey("dueDate") == true ? task.Attributes["dueDate"] : null
        };
        trace.Add($"UserTaskCreated: {userTaskId} for task {task.Id}");
        _ = PersistUserTaskAsync(userTaskId, userTaskDetails);
        trace.Add($"UserTaskPaused: Waiting for completion of task {task.Id}");
        return null;
    }

    private async Task HandleZeebeAttributesAsync(BpmnTask task, BpmnModel model, List<string> trace)
    {
        if (task.Attributes == null) return;
        if (task.Attributes.TryGetValue("zeebe:taskDefinition", out var taskDefinitionType) &&
            !string.IsNullOrWhiteSpace(taskDefinitionType))
        {
            trace.Add($"ZeebeTaskDefinition: {taskDefinitionType}");
            try
            {
                if (_serviceTaskRegistry.TryResolve(taskDefinitionType, out var handler))
                {
                    await handler.ExecuteAsync(task.Attributes,
                        model.ProcessVariables ?? new Dictionary<string, object>(), CancellationToken.None);
                    trace.Add($"ZeebeTaskCompleted: {task.Id}");
                }
            }
            catch (Exception ex)
            {
                trace.Add($"ZeebeTaskError: {task.Id} => {ex.GetType().Name}: {ex.Message}");
                _logger.LogError(ex, "Error executing Zeebe task {TaskId}", task.Id);
            }
        }

        if (task.Attributes.TryGetValue("zeebe:ioMapping", out var ioMappingJson))
            try
            {
                var ioMapping = JsonSerializer.Deserialize<Dictionary<string, string>>(ioMappingJson);
                if (ioMapping != null)
                {
                    var vars = model.ProcessVariables ?? new Dictionary<string, object>();
                    foreach (var (target, source) in ioMapping)
                        vars[target] = source.StartsWith("=") ? EvaluateExpression(source, vars)! : source;
                    trace.Add($"ZeebeIOMappingApplied: {task.Id}");
                }
            }
            catch (Exception ex)
            {
                trace.Add($"ZeebeIOMappingError: {task.Id} => {ex.Message}");
                _logger.LogError(ex, "Error processing Zeebe I/O mapping for task {TaskId}", task.Id);
            }
    }

    private async Task PersistUserTaskAsync(string userTaskId, object userTaskDetails)
    {
        _logger.LogInformation("Persisting user task: {UserTaskId} with details: {UserTaskDetails}", userTaskId,
            userTaskDetails);
        await Task.CompletedTask;
    }

    public async Task CompleteUserTaskAsync(string userTaskId, IDictionary<string, object> processVariables)
    {
        _logger.LogInformation("Completing user task: {UserTaskId}", userTaskId);
        await Task.CompletedTask;
    }

    private object? EvaluateExpression(string expression, IDictionary<string, object> variables)
    {
        return variables.TryGetValue(expression.TrimStart('='), out var value) ? value : null;
    }

    private async Task HandleUnregisteredServiceTaskAsync(BpmnTask task, IDictionary<string, object>? processVariables,
        List<string> trace)
    {
        trace.Add($"ExecutingDefaultHandler: {task.Id}");
        if (task.Attributes != null)
            foreach (var attribute in task.Attributes)
                trace.Add($"Attribute: {attribute.Key} = {attribute.Value}");
        var resultVariable = task.Attributes?.ContainsKey("resultVariable") == true
            ? task.Attributes["resultVariable"]
            : $"{task.Id}_Result";
        var result = $"Default result for task {task.Id}";
        processVariables ??= new Dictionary<string, object>();
        processVariables[resultVariable] = result;
        trace.Add($"DefaultResultSet: {task.Id} => {result}");
        await Task.CompletedTask;
    }

    private void CheckAndHandleBoundaryEvents(string activityId, BpmnModel model, List<string> trace)
    {
        var boundaryEvents = model.Events.Where(e => e.Type == "boundaryEvent" && GetAttachedTo(e) == activityId);
        foreach (var boundaryEvent in boundaryEvents)
            if (ShouldTriggerBoundaryEvent(boundaryEvent))
            {
                trace.Add($"BoundaryEvent: {boundaryEvent.Id} triggered on {activityId}");
                if (IsCompensationEvent(boundaryEvent))
                {
                    HandleCompensationEvent(boundaryEvent, activityId, trace);
                }
                else
                {
                    var isInterrupting = DetermineBoundaryEventType(boundaryEvent);
                    trace.Add(isInterrupting
                        ? $"ActivityInterrupted: {activityId} by {boundaryEvent.Id}"
                        : $"NonInterruptingBoundaryEvent: {boundaryEvent.Id} on {activityId}");
                }
            }
    }

    // IProcessEngine interface required methods (already functionally present but ensuring explicit signatures)
    Task<List<string>> IProcessEngine.ExecuteAsync(BpmnModel model, CancellationToken cancellationToken)
    {
        return ExecuteAsync(model, cancellationToken);
    }

    List<string> IProcessEngine.Execute(BpmnModel model)
    {
        return Execute(model);
    }

    public Task<List<string>> ExecuteCaseAsync(CaseModel model, CancellationToken cancellationToken = default)
    {
        var trace = new List<string>
        {
            "CaseExecutionNotSupported: TokenEngine does not support CMMN case execution",
            $"CaseId: {model.Id}",
            $"PlanItems: {model.PlanItems.Count}",
            "Recommendation: Use DistributedTokenEngine (IDistributedProcessEngine) for CMMN support"
        };
        return Task.FromResult(trace);
    }

    public Task<List<string>> ExecuteProcessAsync(string processId, CancellationToken cancellationToken = default)
    {
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
        return Task.FromResult(true);
    }

    public Task RegisterBpmnModelAsync(string processId, string bpmnXml, CancellationToken cancellationToken = default)
    {
        return Task.FromException(
            new NotSupportedException("TokenEngine does not support model registration. Use DistributedTokenEngine."));
    }

    public Task RegisterCmmnModelAsync(string caseId, string cmmnXml)
    {
        return Task.FromException(
            new NotSupportedException("TokenEngine does not support CMMN. Use DistributedTokenEngine."));
    }

    public Task RegisterDmnModelAsync(string decisionId, string dmnXml)
    {
        return Task.FromException(
            new NotSupportedException("TokenEngine does not support DMN registration. Use DistributedTokenEngine."));
    }

    public Task<CaseModel> GetCmmnModelAsync(string caseId)
    {
        return Task.FromException<CaseModel>(
            new NotSupportedException("TokenEngine does not support CMMN. Use DistributedTokenEngine."));
    }

    public Task<List<HistoricalCaseData>> GetHistoricalCaseDataAsync(string caseId)
    {
        return Task.FromException<List<HistoricalCaseData>>(
            new NotSupportedException("TokenEngine does not support historical data. Use DistributedTokenEngine."));
    }

    private void HandleMultiInstanceSubprocess(BpmnSubprocess subprocess, BpmnModel model, List<string> trace)
    {
        var mi = subprocess.Loop as MultiInstanceLoopCharacteristics;
        trace.Add($"MultiInstance: {subprocess.Id}");
        var totalInstances = mi?.LoopCardinality ?? 3;
        var isSequential = mi?.IsSequential ?? false;
        var context = new MultiInstanceContext(subprocess.Id, totalInstances, 0, isSequential);
        _multiInstanceContexts[subprocess.Id] = context;
        if (isSequential)
        {
            trace.Add($"SequentialMultiInstance: {subprocess.Id}");
            for (var i = 0; i < totalInstances; i++)
            {
                trace.Add($"SequentialInstance: {subprocess.Id} instance {i + 1}/{totalInstances}");
                context = context with {CompletedInstances = i + 1};
                _multiInstanceContexts[subprocess.Id] = context;
            }
        }
        else
        {
            trace.Add($"ParallelMultiInstance: {subprocess.Id}");
            for (var i = 0; i < totalInstances; i++)
                trace.Add($"ParallelInstance: {subprocess.Id} instance {i + 1}/{totalInstances}");
            context = context with {CompletedInstances = totalInstances};
            _multiInstanceContexts[subprocess.Id] = context;
        }

        trace.Add($"MultiInstanceCompleted: {subprocess.Id}");
    }

    private void HandleEventSubprocess(BpmnSubprocess subprocess, BpmnModel model, List<string> trace)
    {
        trace.Add($"EventSubprocessTriggering: {subprocess.Id}");
        var subprocessStartEvents = model.Events
            .Where(e => e.Type == "startEvent" && IsWithinSubprocess(e.Id, subprocess.Id, model)).ToList();
        foreach (var startEvent in subprocessStartEvents)
        {
            trace.Add($"EventSubprocessStart: {startEvent.Id}");
            trace.Add($"EventType: {startEvent.Type}");
        }

        trace.Add($"EventSubprocessExecution: {subprocess.Id}");
        var subprocessEndEvents = model.Events
            .Where(e => e.Type == "endEvent" && IsWithinSubprocess(e.Id, subprocess.Id, model)).ToList();
        foreach (var endEvent in subprocessEndEvents) trace.Add($"EventSubprocessEnd: {endEvent.Id}");
    }

    private bool IsWithinSubprocess(string elementId, string subprocessId, BpmnModel model)
    {
        return elementId.StartsWith(subprocessId + "_");
    }

    private static bool IsMultiInstance(BpmnSubprocess sp)
    {
        return sp.Loop is MultiInstanceLoopCharacteristics;
    }

    private static bool IsCompensationEvent(BpmnEvent e)
    {
        return e.Definitions.Any(d => d is CompensationEventDefinition);
    }

    private static string? GetAttachedTo(BpmnEvent e)
    {
        if (e.ExtensionAttributes == null) return null;
        if (e.ExtensionAttributes.TryGetValue("attachedToRef", out var v)) return v;
        if (e.ExtensionAttributes.TryGetValue("attachedTo", out v)) return v;
        return null;
    }

    private bool ShouldTriggerBoundaryEvent(BpmnEvent boundaryEvent)
    {
        return boundaryEvent.Type switch
        {
            "timer" => false, "message" => false, "error" => false, "signal" => false, "compensation" => false,
            _ => false
        };
    }

    private void HandleCompensationEvent(BpmnEvent compensationEvent, string activityId, List<string> trace)
    {
        trace.Add($"CompensationTriggered: {compensationEvent.Id}");
        _compensationStack[compensationEvent.Id] = new CompensationContext(compensationEvent.Id, activityId);
        trace.Add($"CompensationHandler: {compensationEvent.Id} attached to {activityId}");
    }

    private bool DetermineBoundaryEventType(BpmnEvent boundaryEvent)
    {
        return boundaryEvent.Definitions.Any(d => d is CancelEventDefinition);
    }

    private List<BpmnSequenceFlow> EvaluateComplexGatewayConditions(BpmnGateway gateway, List<BpmnSequenceFlow> flows,
        List<string> trace)
    {
        trace.Add($"EvaluatingComplexConditions: {gateway.Id}");
        var selectedFlows = new List<BpmnSequenceFlow>();
        if (flows.Count > 0)
        {
            selectedFlows.Add(flows[0]);
            if (flows.Count > 1) selectedFlows.Add(flows[1]);
        }

        trace.Add($"ComplexGatewayResult: {selectedFlows.Count} flows selected");
        return selectedFlows;
    }

    private BpmnSequenceFlow? SelectEventBasedFlow(BpmnGateway gateway, List<BpmnSequenceFlow> flows, BpmnModel model,
        List<string> trace)
    {
        trace.Add($"WaitingForEvents: {gateway.Id}");
        foreach (var flow in flows)
        {
            var targetEvent = model.Events.FirstOrDefault(e => e.Id == flow.TargetRef);
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

        trace.Add("NoEventTriggered: selecting default flow");
        return flows.FirstOrDefault();
    }

    private bool SimulateEventArrival(BpmnEvent targetEvent)
    {
        _logger.LogInformation("Simulating event arrival for event: {EventId} of type: {EventType}", targetEvent.Id,
            targetEvent.Type);
        return targetEvent.Type switch
        {
            "message" => SimulateMessageEvent(targetEvent), "timer" => SimulateTimerEvent(targetEvent),
            "signal" => SimulateSignalEvent(targetEvent), "error" => SimulateErrorEvent(targetEvent),
            "condition" => SimulateConditionEvent(targetEvent), _ => false
        };
    }

    private bool SimulateMessageEvent(BpmnEvent targetEvent)
    {
        var message =
            targetEvent.Definitions.FirstOrDefault(d => d is MessageEventDefinition) as MessageEventDefinition;
        if (message != null && message.CorrelationKey != null && message.CorrelationKey == "expectedCorrelationKey")
        {
            _logger.LogInformation("Message event {EventId} triggered with correlation key: {CorrelationKey}",
                targetEvent.Id, message.CorrelationKey);
            return true;
        }

        _logger.LogWarning("Message event {EventId} not triggered. Correlation key mismatch.", targetEvent.Id);
        return false;
    }

    private bool SimulateTimerEvent(BpmnEvent targetEvent)
    {
        return true;
    }

    private bool SimulateSignalEvent(BpmnEvent targetEvent)
    {
        var signal = targetEvent.Definitions.FirstOrDefault(d => d is SignalEventDefinition) as SignalEventDefinition;
        if (signal != null && signal.SignalRef == "expectedSignal")
        {
            _logger.LogInformation("Signal event {EventId} triggered with signal name: {SignalName}", targetEvent.Id,
                signal.SignalRef);
            return true;
        }

        _logger.LogWarning("Signal event {EventId} not triggered. Signal name mismatch.", targetEvent.Id);
        return false;
    }

    private bool SimulateErrorEvent(BpmnEvent targetEvent)
    {
        var eventDefinition =
            targetEvent.Definitions.FirstOrDefault(d => d is ErrorEventDefinition) as ErrorEventDefinition;
        if (eventDefinition?.ErrorCode == "expectedErrorCode")
        {
            _logger.LogInformation("Error event {EventId} triggered with error code: {ErrorCode}", targetEvent.Id,
                eventDefinition.ErrorCode);
            return true;
        }

        _logger.LogWarning("Error event {EventId} not triggered. Error code mismatch.", targetEvent.Id);
        return false;
    }

    private bool SimulateConditionEvent(BpmnEvent targetEvent)
    {
        var eventDefinition =
            targetEvent.Definitions.FirstOrDefault(d => d is ConditionalEventDefinition) as ConditionalEventDefinition;
        if (eventDefinition != null && eventDefinition.Condition == "expectedCondition")
        {
            _logger.LogInformation("Condition event {EventId} triggered. Condition is met.", targetEvent.Id);
            return true;
        }

        _logger.LogWarning("Condition event {EventId} not triggered. Condition not met.", targetEvent.Id);
        return false;
    }
}