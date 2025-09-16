using System.Collections.Concurrent;
using Jint;
using Microsoft.Extensions.Logging;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Entities.Modeling;
using VertexBPMN.Domain.Exceptions;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Infrastructure.Scripting;

namespace VertexBPMN.Engine.Execution;

/// <summary>
/// ProposalTokenEngine
/// Lightweight, purely local in-process engine implementing IProcessEngine.
/// Intended for development, unit tests, single-node demos.
/// - No worker registry / distribution
/// - No messaging layer
/// - Optional DMN evaluation if parsers provided
/// - In-memory model registry (BPMN/DMN/CMMN)
/// - Deterministic synchronous execution loop with async facade
/// </summary>
public class ProposalTokenEngine : IProcessEngine
{
    private readonly ILogger<ProposalTokenEngine> _logger;
    private readonly IServiceTaskRegistry _serviceTaskRegistry;
    private readonly IDmnParser? _dmnParser;
    private readonly IDmnEngine? _dmnEngine;
    private readonly IBpmnParser? _bpmnParser;
    private readonly ICmmnParser? _cmmnParser;

    // In-memory registries (thread-safe)
    private readonly ConcurrentDictionary<string,string> _bpmnModels = new();
    private readonly ConcurrentDictionary<string,string> _cmmnModels = new();
    private readonly ConcurrentDictionary<string,string> _dmnModels  = new();

    private readonly Jint.Engine _scriptEngine = new(); // optional re-use for simple condition evaluations

    public ProposalTokenEngine(
        ILogger<ProposalTokenEngine> logger,
        IServiceTaskRegistry serviceTaskRegistry,
        IBpmnParser? bpmnParser = null,
        IDmnParser? dmnParser = null,
        IDmnEngine? dmnEngine = null,
        ICmmnParser? cmmnParser = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _serviceTaskRegistry = serviceTaskRegistry ?? throw new ArgumentNullException(nameof(serviceTaskRegistry));
        _bpmnParser = bpmnParser;
        _dmnParser = dmnParser;
        _dmnEngine = dmnEngine;
        _cmmnParser = cmmnParser;
    }

    #region IProcessEngine Core
    public async Task<List<string>> ExecuteAsync(BpmnModel model, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await Task.Run(() => ExecuteInternal(model, cancellationToken), cancellationToken);
    }

    public List<string> Execute(BpmnModel model) => ExecuteInternal(model, CancellationToken.None);

    public Task<List<string>> ExecuteCaseAsync(CaseModel model, CancellationToken cancellationToken = default)
    {
        // Minimal placeholder – no full CMMN state machine here
        var trace = new List<string>
        {
            "CaseExecutionLocalMode: Simplified",
            "Note: Full CMMN behavior requires distributed engine"
        };
        // Trigger plan items without entry sentries in a simple sequential fashion
        foreach (var plan in model.PlanItems.Where(p => p.EntrySentryRefs == null || p.EntrySentryRefs.Count == 0))
        {
            trace.Add($"PlanItemStarted: {plan.Id} ({plan.Type})");
            trace.Add($"PlanItemCompleted: {plan.Id}");
        }
        return Task.FromResult(trace);
    }

    public async Task<List<string>> ExecuteProcessAsync(string processId, CancellationToken cancellationToken = default)
    {
        if (!_bpmnModels.TryGetValue(processId, out var xml))
            throw new DistributedTokenException($"BPMN model '{processId}' not registered");
        if (_bpmnParser == null)
            throw new DistributedTokenException("No BPMN parser provided to ProposalTokenEngine");
        var model = await _bpmnParser.ParseAsync(xml, cancellationToken);
        return await ExecuteAsync(model, cancellationToken);
    }

    public Task<bool> CanExecuteAsync(string nodeId, CancellationToken cancellationToken = default)
        => Task.FromResult(true); // Always true in local mode
    #endregion

    #region Model Registration & Retrieval
    public Task RegisterBpmnModelAsync(string processId, string bpmnXml, CancellationToken cancellationToken = default)
    {
        _bpmnModels[processId] = bpmnXml;
        _logger.LogInformation("BPMN model registered (local) {ProcessId}", processId);
        return Task.CompletedTask;
    }

    public Task RegisterCmmnModelAsync(string caseId, string cmmnXml)
    {
        _cmmnModels[caseId] = cmmnXml;
        _logger.LogInformation("CMMN model registered (local) {CaseId}", caseId);
        return Task.CompletedTask;
    }

    public async Task RegisterDmnModelAsync(string decisionId, string dmnXml)
    {
        // Parse once for validation if parser available
        if (_dmnParser != null)
        {
            try { await _dmnParser.ParseAsync(dmnXml); }
            catch (Exception ex) { throw new DistributedTokenException($"Invalid DMN XML for {decisionId}", ex); }
        }
        _dmnModels[decisionId] = dmnXml;
        _logger.LogInformation("DMN model registered (local) {DecisionId}", decisionId);
    }

    public async Task<CaseModel> GetCmmnModelAsync(string caseId)
    {
        if (!_cmmnModels.TryGetValue(caseId, out var xml))
            throw new DistributedTokenException($"CMMN model '{caseId}' not registered");
        if (_cmmnParser == null)
            throw new DistributedTokenException("No CMMN parser provided to ProposalTokenEngine");
        return await _cmmnParser.ParseAsync(xml);
    }

    public Task<List<HistoricalCaseData>> GetHistoricalCaseDataAsync(string caseId)
    {
        // Local engine has no persistence – return empty
        return Task.FromResult(new List<HistoricalCaseData>());
    }
    #endregion

    #region BPMN Execution (Simplified)
    private List<string> ExecuteInternal(BpmnModel model, CancellationToken ct)
    {
        var trace=new List<string>();
        var start=model.Events.FirstOrDefault(e=>e.Type=="startEvent")??throw new BpmnEngineException("No startEvent found");
        trace.Add($"StartEvent: {start.Id}");

        // Worklist (queue) for breadth-first style token processing
        var q=new Queue<ExecutionToken>();
        q.Enqueue(new ExecutionToken(Guid.NewGuid(),Guid.NewGuid(),start.Id,start.Type,new(model.ProcessVariables??new()),DateTime.UtcNow));

        int guard=0; const int max=2000;
        while(q.Count>0 && guard<max && !ct.IsCancellationRequested)
        {
            guard++;
            var token=q.Dequeue();
            var node=FindNode(model, token.CurrentNodeId);
            if(node==null)
            {
                trace.Add($"NodeNotFound: {token.CurrentNodeId}");
                continue;
            }
            switch(node)
            {
                case BpmnEvent evt: HandleEvent(evt,token,model,trace,q); break;
                case BpmnTask task: HandleTask(task,token,model,trace,q).GetAwaiter().GetResult(); break;
                case BpmnGateway gw: HandleGateway(gw,token,model,trace,q); break;
                case BpmnSubprocess sp: HandleSubprocess(sp,token,model,trace,q); break;
            }
        }

        if(guard>=max) trace.Add("ExecutionLimitReached");

        return trace;
    }

    private object? FindNode(BpmnModel model,string id)
    {
        var evt=model.Events.FirstOrDefault(e=>e.Id==id); if(evt!=null) return evt;
        var task=model.Tasks.FirstOrDefault(t=>t.Id==id); if(task!=null) return task;
        var gw=model.Gateways.FirstOrDefault(g=>g.Id==id); if(gw!=null) return gw;
        return model.Subprocesses.FirstOrDefault(s=>s.Id==id);
    }

    private void EnqueueNext(string currentId,ExecutionToken token,BpmnModel model,Queue<ExecutionToken> q,List<string> trace)
    {
        foreach(var f in model.SequenceFlows.Where(f=>f.SourceRef==currentId)) {
            trace.Add($"SequenceFlow: {f.Id}");
            q.Enqueue(token with { CurrentNodeId=f.TargetRef, NodeType=GetNodeType(model,f.TargetRef)});
        }
    }

    private string GetNodeType(BpmnModel model,string nodeId)
    {
        if(model.Events.Any(e=>e.Id==nodeId)) return model.Events.First(e=>e.Id==nodeId).Type;
        if(model.Tasks.Any(t=>t.Id==nodeId)) return model.Tasks.First(t=>t.Id==nodeId).Type;
        if(model.Gateways.Any(g=>g.Id==nodeId)) return model.Gateways.First(g=>g.Id==nodeId).Type;
        if(model.Subprocesses.Any(s=>s.Id==nodeId)) return "subprocess";
        return "unknown";
    }

    private void HandleEvent(BpmnEvent evt,ExecutionToken token,BpmnModel model,List<string> trace,Queue<ExecutionToken> q)
    {
        switch(evt.Type)
        {
            case "startEvent":
                trace.Add($"StartPassed: {evt.Id}");
                EnqueueNext(evt.Id,token,model,q,trace);
                break;
            case "endEvent":
                trace.Add($"EndEvent: {evt.Id}");
                break;
            default:
                trace.Add($"Event: {evt.Type} {evt.Id}");
                EnqueueNext(evt.Id,token,model,q,trace);
                break;
        }
    }

    private async Task HandleTask(BpmnTask task,ExecutionToken token,BpmnModel model,List<string> trace,Queue<ExecutionToken> q)
    {
        switch(task.Type.ToLowerInvariant())
        {
            case "scripttask":
                if(model.ProcessVariables!=null) foreach(var kv in model.ProcessVariables) token.Variables[kv.Key]=kv.Value;
                await ScriptTaskExecution.TryHandleScriptTaskAsync(task, token.Variables, CancellationToken.None);
                model.ProcessVariables ??= new();
                foreach(var kv in token.Variables) model.ProcessVariables[kv.Key]=kv.Value;
                trace.Add($"ScriptTaskCompleted: {task.Id}");
                break;
            case "servicetask":
                await HandleServiceTaskAsync(task,token,model,trace);
                break;
            case "businessruletask":
                await HandleBusinessRuleTaskAsync(task,token,model,trace);
                break;
            case "usertask":
                trace.Add($"UserTask(parked): {task.Id}");
                // local mode: do not auto-complete user tasks
                return; // stop propagation
            default:
                trace.Add($"Task: {task.Type} {task.Id}");
                break;
        }
        EnqueueNext(task.Id,token,model,q,trace);
    }

    private async Task HandleServiceTaskAsync(BpmnTask task,ExecutionToken token,BpmnModel model,List<string> trace)
    {
        var impl=task.Implementation ?? task.Attributes?.GetValueOrDefault("implementation");
        if(string.IsNullOrWhiteSpace(impl))
        {
            trace.Add($"ServiceTaskNoImplementation: {task.Id}");
            return;
        }
        if(_serviceTaskRegistry.TryResolve(impl,out var handler))
        {
            await handler.ExecuteAsync(task.Attributes??new(), token.Variables, CancellationToken.None);
            trace.Add($"ServiceTaskCompleted: {task.Id}");
        }
        else
        {
            trace.Add($"ServiceTaskHandlerNotFound: {impl}");
        }
        model.ProcessVariables ??= new();
        foreach(var kv in token.Variables) model.ProcessVariables[kv.Key]=kv.Value;
    }

    private async Task HandleBusinessRuleTaskAsync(BpmnTask task,ExecutionToken token,BpmnModel model,List<string> trace)
    {
        var attrs=task.Attributes??new Dictionary<string,string>();
        if(!attrs.TryGetValue("camunda:decisionRef",out var decisionRef)&&
           !attrs.TryGetValue("flowable:decisionRef",out decisionRef))
        {
            trace.Add($"BusinessRuleTaskNoDecision: {task.Id}");
            return;
        }
        var resultVar= attrs.GetValueOrDefault("camunda:resultVariable")
                      ?? attrs.GetValueOrDefault("flowable:resultVariable")
                      ?? "decisionResult";
        if(!_dmnModels.TryGetValue(decisionRef,out var dmnXml) || _dmnParser==null || _dmnEngine==null)
        {
            trace.Add($"BusinessRuleLocalFallback: {task.Id} decisionRef={decisionRef} unavailable");
            token.Variables[resultVar]="unavailable";
            return;
        }
        try
        {
            var decision= await _dmnParser.ParseAsync(dmnXml, CancellationToken.None);
            var eval= await _dmnEngine.EvaluateDecisionAsync(decision, token.Variables);
            token.Variables[resultVar]=eval;
            model.ProcessVariables ??= new();
            model.ProcessVariables[resultVar]=eval;
            trace.Add($"BusinessRuleEvaluated: {task.Id}->{resultVar}");
        }
        catch(Exception ex)
        {
            trace.Add($"BusinessRuleError: {task.Id} {ex.Message}");
            _logger.LogError(ex, "DMN evaluation failed for task {TaskId}", task.Id);
        }
    }

    private void HandleGateway(BpmnGateway gw,ExecutionToken token,BpmnModel model,List<string> trace,Queue<ExecutionToken> q)
    {
        var flows=model.SequenceFlows.Where(f=>f.SourceRef==gw.Id).ToList();
        switch(gw.Type)
        {
            case "parallelGateway":
                trace.Add($"ParallelGateway: {gw.Id}");
                foreach(var f in flows)
                {
                    trace.Add($"ParallelBranch: {f.TargetRef}");
                    q.Enqueue(token with { CurrentNodeId=f.TargetRef, NodeType=GetNodeType(model,f.TargetRef)});
                }
                break;
            case "exclusiveGateway":
                var first=flows.FirstOrDefault();
                if(first!=null)
                {
                    trace.Add($"ExclusiveSelected: {first.TargetRef}");
                    q.Enqueue(token with { CurrentNodeId=first.TargetRef, NodeType=GetNodeType(model,first.TargetRef)});
                }
                break;
            case "inclusiveGateway":
                trace.Add($"InclusiveGateway: {gw.Id}");
                foreach(var f in flows)
                {
                    trace.Add($"InclusiveBranch: {f.TargetRef}");
                    q.Enqueue(token with { CurrentNodeId=f.TargetRef, NodeType=GetNodeType(model,f.TargetRef)});
                }
                break;
            default:
                trace.Add($"GatewayUnsupported: {gw.Type} {gw.Id}");
                break;
        }
    }

    private void HandleSubprocess(BpmnSubprocess sp,ExecutionToken token,BpmnModel model,List<string> trace,Queue<ExecutionToken> q)
    {
        trace.Add($"Subprocess: {sp.Id}");
        if(sp.IsMultiInstance)
        {
            var count= sp.LoopCardinality ?? 1;
            for(int i=0;i<count;i++)
            {
                trace.Add($"MultiInstanceSubprocessInstance: {sp.Id}#{i+1}");
            }
        }
        EnqueueNext(sp.Id,token,model,q,trace);
    }
    #endregion
}
