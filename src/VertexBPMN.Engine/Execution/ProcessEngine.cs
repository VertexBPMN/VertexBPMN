using System.Collections;
using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using VertexBPMN.Application;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Domain.Model.Bpmn;
using VertexBPMN.Domain.Model.Cmn;
using VertexBPMN.Domain.Model.Dmn;

namespace VertexBPMN.Engine.Execution;

public partial class ProcessEngine : IProcessEngine
{
    private readonly ILogger<ProcessEngine> _logger;
    private readonly IDecisionService? _decisionService; // optional DMN service
    private readonly IBpmnParser? _bpmnParser;
    private readonly IDmnParser? _dmnParser;
    private readonly IDmnEngine? _dmnEngine;
    private readonly ICmmnParser? _cmmnParser;
    private readonly IAiDecisionService? _aiDecisionService;
    private readonly IServiceTaskRegistry _serviceTaskRegistry;
    private readonly BpmnExecutionComponent _executionComponent = new();
    private readonly ConcurrentDictionary<string, BpmnModel> _registeredModels = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, DmnDecision> _registeredDecisions = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, CaseModel> _registeredCases = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, Dictionary<string, object>> _caseFiles = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, List<HistoricalCaseData>> _caseHistory = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, LocalExecutionHistory> _executionHistory = new(StringComparer.OrdinalIgnoreCase);
    private string? _lastExecutionId;

    public sealed record LocalExecutionHistory(
        string ExecutionId,
        string ProcessId,
        DateTime StartedAt,
        DateTime CompletedAt,
        IReadOnlyList<string> Trace,
        IReadOnlyList<string> History);
    private Dictionary<string, object>? _workingVariables;
    // ----- Token & History layer (added) -----
    private record Token(string Id, string CurrentNodeId, string? ParentTxn, bool Active);
    private record HistoryEvent(DateTime Ts, string TokenId, string NodeId, string Action, string? Detail = null);
    private long _tokenSeq;
    private readonly Dictionary<string, Token> _tokens = new();
    private readonly Queue<(string TokenId, string NodeId, string? FromFlow, string? ParentTxn)> _tokenQueue = new();
    private readonly List<HistoryEvent> _history = new();
    private readonly HashSet<string> _pendingNodes = new(); // nodes currently enqueued (for join reachability heuristics)
    private string? _lastErrorCode;
    // NEU: Analog zu _lastErrorCode – merkt sich, ob/welches Message-, Escalation- oder
    // Signal-Throw-Event zuletzt aufgetreten ist. Ohne das feuert jedes an eine Aktivität
    // angehängte non-error Boundary Event bedingungslos beim bloßen Erreichen des Knotens.
    private string? _lastMessageRef;
    private string? _lastEscalationCode;
    private string? _lastSignalRef;
    private class JoinContext
    {
        public HashSet<string> RequiredFlows { get; } = new();
        public HashSet<string> ArrivedFlows { get; } = new();
        public bool Fired; // to avoid double firing
    }
    private readonly Dictionary<string, JoinContext> _joinContexts = new();

    // FIX: invalid numeric format specifier replaced with composite formatting
    private string NextTokenId()
    {
        var seq = Interlocked.Increment(ref _tokenSeq);
        return $"T{seq:D4}"; // e.g. T0001, T0002...
    }

    private void LogHistory(string tokenId, string nodeId, string action, string? detail = null)
        => _history.Add(new HistoryEvent(DateTime.UtcNow, tokenId, nodeId, action, detail));

    // expose history (optional future use)
    public IReadOnlyList<string> GetHistoryTrace() => _history.Select(h => $"{h.Ts:O}|{h.TokenId}|{h.NodeId}|{h.Action}|{h.Detail}").ToList();

    public string? LastExecutionId => _lastExecutionId;

    public bool TryGetExecutionHistory(string executionId, out LocalExecutionHistory? history)
        => _executionHistory.TryGetValue(executionId, out history);

    public ProcessEngine() : this(NullLogger<ProcessEngine>.Instance,
        NullServiceTaskRegistry.Instance)
    {
    }

    public ProcessEngine(ILogger<ProcessEngine> logger,
        IServiceTaskRegistry serviceTaskRegistry, IDecisionService? decisionService = null,
        IBpmnParser? bpmnParser = null, IDmnParser? dmnParser = null, IDmnEngine? dmnEngine = null,
        ICmmnParser? cmmnParser = null, IAiDecisionService? aiDecisionService = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _serviceTaskRegistry = serviceTaskRegistry ?? throw new ArgumentNullException(nameof(serviceTaskRegistry));
        _decisionService = decisionService; // may be null
        _bpmnParser = bpmnParser;
        _dmnParser = dmnParser;
        _dmnEngine = dmnEngine;
        _cmmnParser = cmmnParser;
        _aiDecisionService = aiDecisionService;
    }

    // Runtime state
    private readonly Dictionary<string, HashSet<string>> _parallelJoinArrivals = new();
    private readonly Dictionary<string, HashSet<string>> _inclusiveJoinArrivals = new();
    private readonly Dictionary<string, List<BpmnSequenceFlow>> _incomingByTarget = new();
    private readonly Dictionary<string, TransactionContext> _transactionContexts = new();
    private readonly Dictionary<string, List<string>> _eventSubprocessStartEvents = new();
    private readonly HashSet<string> _disabledFlows = new(); // dead path elimination marker (flow ids)

    private record TransactionContext(string Id, HashSet<string> ActiveTokenIds, bool Terminated = false);

    #region Local Registries and Optional Features

    public async Task RegisterBpmnModelAsync(string processId, string bpmnXml, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(processId);
        ArgumentException.ThrowIfNullOrWhiteSpace(bpmnXml);
        cancellationToken.ThrowIfCancellationRequested();

        if (_bpmnParser == null)
        {
            throw new NotSupportedException(
                "BPMN XML registration requires an IBpmnParser. Provide one through the ProcessEngine constructor.");
        }

        var model = await _bpmnParser.ParseAsync(bpmnXml, cancellationToken);
        RegisterBpmnModel(processId, model);
    }

    public void RegisterBpmnModel(string processId, BpmnModel model)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(processId);
        ArgumentNullException.ThrowIfNull(model);
        _registeredModels[processId] = model;
    }

    public async Task RegisterCmmnModelAsync(string caseId, string cmmnXml)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(caseId);
        ArgumentException.ThrowIfNullOrWhiteSpace(cmmnXml);
        if (_cmmnParser == null)
            throw new NotSupportedException("CMMN XML registration requires an ICmmnParser.");

        var model = await _cmmnParser.ParseAsync(cmmnXml);
        _registeredCases[caseId] = model;
        _caseFiles[caseId] = model.CaseFileItems
            .Where(item => !string.IsNullOrWhiteSpace(item.Id))
            .ToDictionary(item => item.Id, item => item.Value ?? new object(), StringComparer.OrdinalIgnoreCase);
    }

    public async Task RegisterDmnModelAsync(string decisionId, string dmnXml)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(decisionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(dmnXml);

        if (_dmnParser == null)
        {
            throw new NotSupportedException(
                "DMN XML registration requires an IDmnParser. Provide one through the ProcessEngine constructor.");
        }

        var decision = await _dmnParser.ParseAsync(dmnXml);
        if (!string.IsNullOrWhiteSpace(decision.SourceXml))
        {
            _ = DmnDecisionGraph.Parse(decision.SourceXml, decisionId);
            decision = decision with { EvaluationTargetId = decisionId };
        }
        _registeredDecisions[decisionId] = decision;
    }

    public Task<CaseModel> GetCmmnModelAsync(string caseId)
    {
        if (_registeredCases.TryGetValue(caseId, out var model))
            return Task.FromResult(model);
        return Task.FromException<CaseModel>(new KeyNotFoundException($"CMMN case '{caseId}' is not registered."));
    }

    public Task<List<HistoricalCaseData>> GetHistoricalCaseDataAsync(string caseId)
    {
        return Task.FromResult(_caseHistory.TryGetValue(caseId, out var history)
            ? history.ToList()
            : new List<HistoricalCaseData>());
    }

    public async Task<List<string>> ExecuteProcessAsync(string processId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(processId);
        cancellationToken.ThrowIfCancellationRequested();

        if (!_registeredModels.TryGetValue(processId, out var model))
        {
            throw new KeyNotFoundException($"BPMN process model '{processId}' is not registered.");
        }

        return await ExecuteAsync(model, cancellationToken);
    }

    public Task<bool> CanExecuteAsync(string nodeId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(true);
    }

    public Task CompleteUserTaskAsync(string userTaskId, IDictionary<string, object> processVariables)
    {
        _logger.LogInformation("Completing user task: {UserTaskId}", userTaskId);
        return Task.CompletedTask;
    }

    public async Task<List<string>> ExecuteCaseAsync(CaseModel model, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(model);
        cancellationToken.ThrowIfCancellationRequested();

        var caseFile = _caseFiles.GetOrAdd(model.Id, _ => model.CaseFileItems
            .Where(item => !string.IsNullOrWhiteSpace(item.Id))
            .ToDictionary(item => item.Id, item => item.Value ?? new object(), StringComparer.OrdinalIgnoreCase));
        var trace = new List<string> { $"LocalCaseExecution: Starting case {model.Id}" };
        var completed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var pending = new Queue<PlanItem>(model.PlanItems.Where(item =>
            !item.IsDiscretionary && (item.EntrySentryRefs == null || item.EntrySentryRefs.Count == 0)));

        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var item = pending.Dequeue();
            if (!completed.Add(item.Id))
                continue;

            trace.Add($"PlanItem: {item.Id} ({item.Type})");
            if (item.Type.Equals("humanTask", StringComparison.OrdinalIgnoreCase) ||
                item.Type.Equals("userTask", StringComparison.OrdinalIgnoreCase))
                trace.Add($"UserTask: {item.Id}");
            else if (item.Type.Equals("serviceTask", StringComparison.OrdinalIgnoreCase))
                trace.Add($"ServiceTask: {item.Id}");
            else if (item.Type.Equals("eventListener", StringComparison.OrdinalIgnoreCase))
                trace.Add($"EventListener: {item.Id}");
            else
                trace.Add($"PlanItemCompleted: {item.Id}");

            foreach (var dependent in model.PlanItems.Where(candidate =>
                !completed.Contains(candidate.Id) &&
                candidate.EntrySentryRefs?.Any(reference => model.Sentries.Any(sentry =>
                    sentry.Id == reference && sentry.OnPartRef == item.Id)) == true))
            {
                pending.Enqueue(dependent);
            }
        }

        var history = new HistoricalCaseData(model.Id, new Dictionary<string, object>(caseFile), completed.ToList(), DateTime.UtcNow);
        _caseHistory.AddOrUpdate(model.Id, _ => new List<HistoricalCaseData> { history }, (_, entries) =>
        {
            entries.Add(history);
            return entries;
        });
        await Task.CompletedTask;
        return trace;
    }

    public Task AddDiscretionaryItemAsync(string caseId, PlanItem planItem, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(planItem);
        cancellationToken.ThrowIfCancellationRequested();
        if (!_registeredCases.TryGetValue(caseId, out var model))
            return Task.FromException(new KeyNotFoundException($"CMMN case '{caseId}' is not registered."));
        if (!planItem.IsDiscretionary)
            return Task.FromException(new ArgumentException("Plan item must be discretionary.", nameof(planItem)));

        _registeredCases[caseId] = model with { PlanItems = new List<PlanItem>(model.PlanItems) { planItem } };
        return Task.CompletedTask;
    }

    public Task UpdateCaseFileItemAsync(string caseId, string caseFileItemId, object newValue, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_caseFiles.TryGetValue(caseId, out var caseFile))
            return Task.FromException(new KeyNotFoundException($"CMMN case '{caseId}' is not registered."));
        caseFile[caseFileItemId] = newValue;
        return Task.CompletedTask;
    }

    public Task TriggerUserEventAsync(string caseId, string eventId, Dictionary<string, object> eventData, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_caseFiles.TryGetValue(caseId, out var caseFile))
            return Task.FromException(new KeyNotFoundException($"CMMN case '{caseId}' is not registered."));
        foreach (var entry in eventData)
            caseFile[entry.Key] = entry.Value;
        return Task.CompletedTask;
    }

    public async Task GenerateAdHocSubprocessAsync(string caseId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_caseFiles.TryGetValue(caseId, out var caseFile))
            throw new KeyNotFoundException($"CMMN case '{caseId}' is not registered.");

        PlanItem item;
        if (_aiDecisionService != null)
        {
            var predictionTask = _aiDecisionService.PredictOptimalPlanItemsAsync(
                caseId,
                new Dictionary<string, object>(caseFile),
                await GetHistoricalCaseDataAsync(caseId),
                cancellationToken);
            var suggestions = predictionTask is null ? null : await predictionTask;
            item = suggestions?.FirstOrDefault() ?? await GetAdHocFallbackAsync(caseId, caseFile, cancellationToken);
        }
        else
        {
            item = await GetAdHocFallbackAsync(caseId, caseFile, cancellationToken);
        }

        item = item with { IsDiscretionary = true };
        await AddDiscretionaryItemAsync(caseId, item, cancellationToken);
    }

    private async Task<PlanItem> GetAdHocFallbackAsync(string caseId, Dictionary<string, object> caseFile, CancellationToken cancellationToken)
    {
        var generationTask = _aiDecisionService?.GenerateAdHocSubprocessAsync(
            caseId,
            new Dictionary<string, object>(caseFile),
            cancellationToken);
        var generatedItem = generationTask is null ? null : await generationTask;
        return generatedItem ?? new PlanItem($"adhoc-{Guid.NewGuid():N}", "task", "", new Dictionary<string, string>(), new List<string>(), new List<string>(), true);
    }

    #endregion

    public Task<List<string>> ExecuteAsync(BpmnModel model, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Execute(model));
    }

    public List<string> Execute(BpmnModel model)
    {
        return Execute(model, _decisionService);
    }

    public List<string> Execute(BpmnModel model, IDecisionService? decisionService = null)
    {
        return Execute(model, decisionService, null, null);
    }

    /// <summary>
    /// Executes a new process instance with variables that are isolated from the
    /// parsed process definition and from other executions of this engine.
    /// </summary>
    public List<string> Execute(
        BpmnModel model,
        IReadOnlyDictionary<string, object> variables,
        IDecisionService? decisionService = null)
    {
        ArgumentNullException.ThrowIfNull(variables);
        return Execute(model, decisionService ?? _decisionService, null, variables);
    }

    public List<string> ExecuteFromStartEvent(
        BpmnModel model,
        string startEventId,
        IDecisionService? decisionService = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(startEventId);
        return Execute(
            model,
            decisionService ?? _decisionService,
            new HashSet<string>(StringComparer.Ordinal) { startEventId },
            null);
    }

    /// <summary>
    /// Executes a new process instance from an explicitly triggered Start Event
    /// with instance-scoped input variables.
    /// </summary>
    public List<string> ExecuteFromStartEvent(
        BpmnModel model,
        string startEventId,
        IReadOnlyDictionary<string, object> variables,
        IDecisionService? decisionService = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(startEventId);
        ArgumentNullException.ThrowIfNull(variables);
        return Execute(
            model,
            decisionService ?? _decisionService,
            new HashSet<string>(StringComparer.Ordinal) { startEventId },
            variables);
    }

    public List<string> ExecuteTriggeredEventSubprocess(
        BpmnModel model,
        string startEventId,
        IDecisionService? decisionService = null)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentException.ThrowIfNullOrWhiteSpace(startEventId);

        var startEvent = model.Events.FirstOrDefault(e =>
            e.Id == startEventId && e.Type == "startEvent");
        if (startEvent is null || string.IsNullOrWhiteSpace(startEvent.SubprocessId))
            throw new InvalidOperationException(
                $"Requested Event Subprocess Start Event '{startEventId}' was not found.");

        var subprocess = model.Subprocesses.FirstOrDefault(s =>
            s.Id == startEvent.SubprocessId && s.IsEventSubprocess);
        if (subprocess is null || startEvent.Definitions is not { Count: > 0 })
            throw new InvalidOperationException(
                $"Start Event '{startEventId}' is not a typed Event Subprocess Start Event.");

        return Execute(
            model,
            decisionService ?? _decisionService,
            new HashSet<string>(StringComparer.Ordinal) { startEventId },
            null,
            allowEventSubprocessStarts: true);
    }

    private List<string> Execute(
        BpmnModel model,
        IDecisionService? decisionService,
        IReadOnlySet<string>? requestedStartEventIds,
        IReadOnlyDictionary<string, object>? initialVariables,
        bool allowEventSubprocessStarts = false)
    {
        ArgumentNullException.ThrowIfNull(model);
        var executionId = Guid.NewGuid().ToString("N");
        var startedAt = DateTime.UtcNow;
        _lastExecutionId = executionId;
        var trace = new List<string>();
        _tokens.Clear(); _history.Clear(); _tokenQueue.Clear(); _pendingNodes.Clear(); _joinContexts.Clear();
        _parallelJoinArrivals.Clear(); _inclusiveJoinArrivals.Clear(); _transactionContexts.Clear(); _eventSubprocessStartEvents.Clear(); _disabledFlows.Clear();
        // Runtime variables belong to one process instance. Reinitialize them for
        // every execution so a reused engine cannot leak state into the next run.
        // The legacy overload keeps the model dictionary for backward compatibility;
        // explicit inputs are copied and never mutate the static process definition.
        _workingVariables = initialVariables is null
            ? model.ProcessVariables
            : new Dictionary<string, object>(initialVariables, StringComparer.OrdinalIgnoreCase);
        // FIX: _lastErrorCode wurde bisher hier NICHT zurückgesetzt – bei Wiederverwendung
        // derselben ProcessEngine-Instanz für mehrere Execute()-Aufrufe konnte ein Error-Code
        // aus einer vorherigen Ausführung fälschlich in die neue Ausführung durchsickern.
        _lastErrorCode = null; _lastMessageRef = null; _lastEscalationCode = null; _lastSignalRef = null;

        PrecomputeIncoming(model);
        IndexEventSubprocessStartEvents(model, trace);
        var startEvents = requestedStartEventIds is null
            ? model.Events.Where(e =>
                e.Type == "startEvent"
                && e.SubprocessId is null
                && e.Definitions is not { Count: > 0 }
                && (string.IsNullOrWhiteSpace(e.ProcessId)
                    || string.Equals(e.ProcessId, model.ProcessId, StringComparison.Ordinal))).ToList()
            : model.Events.Where(e =>
                e.Type == "startEvent"
                && (allowEventSubprocessStarts || e.SubprocessId is null)
                && requestedStartEventIds.Contains(e.Id)).ToList();
        if (startEvents.Count == 0)
            throw new InvalidOperationException(requestedStartEventIds is null
                ? "No none Start Event exists for automatic process instantiation. Trigger a typed Start Event explicitly."
                : $"Requested Start Event '{string.Join(", ", requestedStartEventIds)}' was not found at process scope.");
        if (requestedStartEventIds is not null && startEvents.Count != requestedStartEventIds.Count)
            throw new InvalidOperationException("One or more requested Start Events were not found at process scope.");
        var linkCatchMap = BuildLinkCatchMap(model.Events);

        // Seed tokens
        foreach (var s in startEvents)
        {
            var tokenId = NextTokenId();
            var tk = new Token(tokenId, s.Id, null, true);
            _tokens[tokenId] = tk;
            _tokenQueue.Enqueue((tokenId, s.Id, null, null));
            _pendingNodes.Add(s.Id);
            trace.Add($"StartEvent: {s.Id} [Token {tokenId}]");
            LogHistory(tokenId, s.Id, "enter", "startEvent");
        }

        var visitCount = new Dictionary<string, int>();
        const int visitLimitPerNode = 100; // slightly higher for token-based

        while (_tokenQueue.Count > 0)
        {
            var (tokenId, currentId, fromFlow, parentTxn) = _tokenQueue.Dequeue();
            _pendingNodes.Remove(currentId);
            if (!_tokens.TryGetValue(tokenId, out var token) || !token.Active) continue;

            if (parentTxn != null && _transactionContexts.TryGetValue(parentTxn, out var tctx) && tctx.Terminated)
            {
                trace.Add($"TransactionTerminatedSkip: {currentId} Token {tokenId} in {parentTxn}");
                LogHistory(tokenId, currentId, "skip", "terminated-txn");
                continue;
            }
            visitCount.TryGetValue(currentId, out var vc);
            if (vc > visitLimitPerNode)
            {
                trace.Add($"VisitLimitReached: {currentId} Token {tokenId}");
                LogHistory(tokenId, currentId, "visit-limit");
                continue;
            }
            visitCount[currentId] = vc + 1;

            // EndEvent handling
            var endEvt = model.Events.FirstOrDefault(e => e.Id == currentId && e.Type == "endEvent");
            if (endEvt != null)
            {
                var evtTypeTerm = GetEventDefinitionType(endEvt);
                var isTerminate = string.Equals(evtTypeTerm, "terminate", StringComparison.OrdinalIgnoreCase) || HasDefinition(endEvt, "terminate");
                trace.Add($"EndEvent: {endEvt.Id}{(isTerminate ? " (terminate)" : string.Empty)} [Token {tokenId}]");
                LogHistory(tokenId, currentId, "end", isTerminate ? "terminate" : null);
                // Capture error code if this is an error end (simple heuristic)
                if (string.Equals(evtTypeTerm, "error", StringComparison.OrdinalIgnoreCase))
                {
                    // Error code via reflection (Parser variant may differ)
                    var errCode = endEvt.GetType().GetProperty("ErrorCode")?.GetValue(endEvt)?.ToString();
                    if (!string.IsNullOrWhiteSpace(errCode))
                    {
                        _lastErrorCode = errCode;
                        trace.Add($"ErrorThrown: {errCode} at {endEvt.Id}");
                    }
                    else
                    {
                        _lastErrorCode = "__error"; // generic marker
                        trace.Add($"ErrorThrown: (generic) at {endEvt.Id}");
                    }
                }
                if (isTerminate)
                {
                    foreach (var k in _transactionContexts.Keys.ToList())
                    {
                        var tx = _transactionContexts[k];
                        if (!tx.Terminated)
                        {
                            _transactionContexts[k] = tx with { Terminated = true };
                            trace.Add($"TransactionForceTerminated: {k} by {endEvt.Id}");
                        }
                    }
                }
                if (parentTxn != null && _transactionContexts.TryGetValue(parentTxn, out var txc))
                {
                    txc.ActiveTokenIds.Remove(tokenId);
                    if (txc.ActiveTokenIds.Count == 0)
                    {
                        trace.Add($"TransactionCompleted: {parentTxn}");
                        _transactionContexts[parentTxn] = txc with { Terminated = true };
                    }
                }
                // deactivate token
                _tokens[tokenId] = token with { Active = false };
                continue;
            }

            // Gateways
            var gateway = model.Gateways.FirstOrDefault(g => g.Id == currentId);
            if (gateway != null)
            {
                bool joinHold = false;
                // FIX: isJoin markiert, ob dieses Gateway überhaupt ein Join ist (>1 eingehender
                // Flow). excessJoinToken markiert einen überzähligen Token NACH bereits erfolgtem
                // Join – der darf das Gateway NICHT nochmal aufspalten lassen (siehe
                // Register*JoinArrivalCompliant). Reine Split-Gateways (kein Join) spalten wie
                // bisher bei jedem Token normal auf.
                var isJoin = false;
                var justFired = true;
                if (IsParallelJoin(gateway))
                {
                    isJoin = true;
                    joinHold = !RegisterParallelJoinArrivalCompliant(gateway.Id, fromFlow, trace, out justFired);
                }
                else if (IsInclusiveJoin(gateway))
                {
                    isJoin = true;
                    joinHold = !RegisterInclusiveJoinArrivalCompliant(gateway.Id, fromFlow, trace, out justFired);
                }
                if (joinHold)
                {
                    LogHistory(tokenId, currentId, "join-wait");
                    continue; // wait for other tokens
                }
                var excessJoinToken = isJoin && !justFired;
                if (excessJoinToken)
                {
                    // Join bereits früher erfüllt – dieser Token wird still konsumiert,
                    // OHNE das Gateway erneut aufzuspalten (verhindert Token-Explosion).
                    if (_tokens.TryGetValue(tokenId, out var excessTk))
                        _tokens[tokenId] = excessTk with { Active = false };
                    LogHistory(tokenId, currentId, "join-excess-consumed");
                    continue;
                }
                var interrupt = ProcessBoundaryEvents(currentId, model, trace, _tokenQueue, parentTxn);
                if (!interrupt) HandleGatewayForToken(gateway, model, trace, tokenId, parentTxn);
                continue;
            }

            // Subprocess
            var subprocess = model.Subprocesses.FirstOrDefault(s => s.Id == currentId);
            if (subprocess != null)
            {
                var interrupt = ProcessBoundaryEvents(currentId, model, trace, _tokenQueue, parentTxn);
                if (!interrupt)
                {
                    var txnId = parentTxn;
                    if (IsTransactionSubprocess(subprocess))
                    {
                        txnId = subprocess.Id;
                        _transactionContexts[txnId] = new TransactionContext(txnId, new HashSet<string> { tokenId });
                        trace.Add($"TransactionStart: {txnId} [Token {tokenId}]");
                    }
                    HandleSubprocess(subprocess, model, trace, _tokenQueue, txnId);
                }
                continue;
            }

            // Task
            var task = model.Tasks.FirstOrDefault(t => t.Id == currentId);
            if (task != null)
            {
                var interrupt = ProcessBoundaryEvents(currentId, model, trace, _tokenQueue, parentTxn);
                if (!interrupt) HandleTaskForToken(task, model, decisionService ?? _decisionService, trace, tokenId, parentTxn);
                continue;
            }

            // Intermediate Event
            var evt = model.Events.FirstOrDefault(e => e.Id == currentId);
            if (evt != null && evt.Type != "startEvent" && evt.Type != "endEvent")
            {
                var interrupt = ProcessBoundaryEvents(currentId, model, trace, _tokenQueue, parentTxn);
                if (!interrupt) HandleIntermediateEvent(evt, trace, _tokenQueue, model, linkCatchMap, parentTxn);
            }

            // Default sequence flows
            foreach (var flow in model.SequenceFlows.Where(f => f.SourceRef == currentId))
            {
                EmitNewToken(trace, tokenId, parentTxn, flow);
            }
        }

        // Append history summary marker for diagnostics
        trace.Add($"HistoryEvents: {_history.Count}");
        _executionHistory[executionId] = new LocalExecutionHistory(
            executionId,
            model.Id,
            startedAt,
            DateTime.UtcNow,
            trace.ToList(),
            GetHistoryTrace());
        return trace;
    }

    private void EmitNewToken(List<string> trace, string sourceTokenId, string? parentTxn, BpmnSequenceFlow flow)
    {
        var newTokenId = NextTokenId();
        var tk = new Token(newTokenId, flow.TargetRef, parentTxn, true);
        _tokens[newTokenId] = tk;
        _tokenQueue.Enqueue((newTokenId, flow.TargetRef, flow.Id, parentTxn));
        _pendingNodes.Add(flow.TargetRef);
        trace.Add($"SequenceFlow: {flow.Id} [Token {sourceTokenId}->{newTokenId}]");
        LogHistory(newTokenId, flow.TargetRef, "enter", flow.Id);
    }

    private void HandleGatewayForToken(BpmnGateway g, BpmnModel model, List<string> trace, string tokenId, string? parentTxn)
    {
        switch (g.Type)
        {
            case "exclusiveGateway":
                trace.Add($"ExclusiveGateway: {g.Id} [Token {tokenId}]");
                break;
            case "parallelGateway":
                trace.Add($"ParallelGateway: {g.Id} [Token {tokenId}]");
                break;
            case "inclusiveGateway":
                trace.Add($"InclusiveGateway: {g.Id} [Token {tokenId}]");
                break;
            case "complexGateway":
                trace.Add($"ComplexGateway: {g.Id} [Token {tokenId}]");
                break;
            case "eventBasedGateway":
                trace.Add($"EventBasedGateway: {g.Id} [Token {tokenId}]");
                break;
            default:
                trace.Add($"Gateway: {g.Id} ({g.Type}) [Token {tokenId}]");
                break;
        }

        var outs = model.SequenceFlows.Where(f => f.SourceRef == g.Id).ToList();

        switch (g.Type)
        {
            case "exclusiveGateway":
                {
                    var vars = GetOrCreateWorkingVariables(model);
                    var decision = _executionComponent.SelectExclusiveFlow(
                        outs,
                        vars,
                        static (flow, variables) => BpmnConditionEvaluator.Evaluate(flow, variables));

                    if (decision.Flow is { } selected)
                    {
                        trace.Add(decision.Kind == GatewayDecisionKind.DefaultSelected
                            ? $"ExclusiveDefaultTaken: {selected.Id}"
                            : $"ExclusiveFlowSelected: {selected.Id}");
                        EmitNewToken(trace, tokenId, parentTxn, selected);
                        foreach (var dead in outs.Where(f => f != selected))
                        {
                            _disabledFlows.Add(dead.Id);
                            trace.Add($"DeadPathEliminated: {dead.Id}");
                        }
                    }
                    else
                    {
                        throw new InvalidOperationException(
                            $"Exclusive Gateway '{g.Id}' has no matching outgoing Sequence Flow and no default Flow.");
                    }
                    break;
                }

            case "parallelGateway":
                foreach (var f in outs)
                {
                    trace.Add($"ParallelBranch: {f.TargetRef} via {f.Id}");
                    EmitNewToken(trace, tokenId, parentTxn, f);
                }
                break;

            case "inclusiveGateway":
                {
                    var vars = GetOrCreateWorkingVariables(model);
                    var selected = _executionComponent.SelectInclusiveFlows(
                        outs,
                        vars,
                        static (flow, variables) => BpmnConditionEvaluator.Evaluate(flow, variables));

                    if (selected.Count == 0)
                    {
                        throw new InvalidOperationException(
                            $"Inclusive Gateway '{g.Id}' has no matching outgoing Sequence Flow and no default Flow.");
                    }

                    foreach (var flow in selected)
                    {
                        trace.Add(flow.IsDefault
                            ? $"InclusiveDefaultTaken: {flow.Id}"
                            : $"InclusiveBranch: {flow.TargetRef} via {flow.Id}");
                        EmitNewToken(trace, tokenId, parentTxn, flow);
                    }

                    foreach (var dead in outs.Except(selected))
                    {
                        _disabledFlows.Add(dead.Id);
                        trace.Add($"DeadPathEliminated: {dead.Id}");
                    }
                    break;
                }

            case "complexGateway":
            case "eventBasedGateway":
            default:
                foreach (var f in outs)
                    EmitNewToken(trace, tokenId, parentTxn, f);
                break;
        }

        if (_tokens.TryGetValue(tokenId, out var tk))
            _tokens[tokenId] = tk with { Active = false };
    }

    // ===== Modify HandleTaskForToken: insert zeebe I/O mapping + unified variable persistence (steps 2,4,5) =====
    private void HandleTaskForToken(BpmnTask task, BpmnModel model, IDecisionService? decisionService, List<string> trace, string tokenId, string? parentTxn)
    {
        switch (task.Type)
        {
            case "userTask":
                trace.Add($"UserTask: {task.Id} [Token {tokenId}]");
                break;
            case "serviceTask":
                trace.Add($"ServiceTask: {task.Id} [Token {tokenId}]");
                break;
            case "scriptTask":
                trace.Add($"ScriptTask: {task.Id} [Token {tokenId}]");
                break;
            case "businessRuleTask":
                trace.Add($"BusinessRuleTask: {task.Id} [Token {tokenId}]");
                break;
            case "receiveTask":
                trace.Add($"ReceiveTask: {task.Id} [Token {tokenId}]");
                break;
            case "sendTask":
                trace.Add($"SendTask: {task.Id} [Token {tokenId}]");
                break;
            case "callActivity":
                trace.Add($"CallActivity: {task.Id} [Token {tokenId}]");
                break;
            default:
                trace.Add($"Task: {task.Id} ({task.Type}) [Token {tokenId}]");
                break;
        }

        var varsRef = GetOrCreateWorkingVariables(model);

        if (task.Type == "serviceTask")
        {
            var implementation = task.Implementation;
            if (string.IsNullOrWhiteSpace(implementation))
            {
                trace.Add($"ServiceTaskNoImplementation: {task.Id}");
            }
            else
            {
                try
                {
                    if (_serviceTaskRegistry.TryResolve(implementation, out var handler) && handler != null)
                    {
                        handler.ExecuteAsync(
                                task.Attributes ?? new Dictionary<string, string>(),
                                varsRef,
                                CancellationToken.None)
                            .GetAwaiter()
                            .GetResult();
                        trace.Add($"ServiceTaskCompleted: {task.Id}");
                    }
                    else
                    {
                        trace.Add($"ServiceTaskHandlerNotFound: {implementation}");
                        _logger.LogWarning("Service task handler not found for implementation: {Implementation}", implementation);
                        ExecuteDefaultServiceTask(task, varsRef, trace);
                    }
                }
                catch (Exception ex)
                {
                    trace.Add($"ServiceTaskError: {task.Id} => {ex.GetType().Name}: {ex.Message}");
                    _logger.LogError(ex, "Error executing service task {TaskId}", task.Id);
                }
            }
        }

        // BusinessRuleTask variable production (simulation or local/remote DMN)
        if (task.Type == "businessRuleTask")
        {
            var decisionKey =
                (task.Attributes != null &&
                 task.Attributes.TryGetValue("decisionRef", out var refKey) &&
                 !string.IsNullOrWhiteSpace(refKey))
                    ? refKey
                    : task.Id;

            try
            {
                if (decisionService != null)
                {
                    var result = decisionService
                        .EvaluateDecisionByKeyAsync(decisionKey, varsRef, null)
                        .GetAwaiter().GetResult();

                    if (result?.Variables != null)
                    {
                        foreach (var kv in result.Variables)
                            varsRef[kv.Key] = kv.Value!;
                    }
                    trace.Add($"DecisionEvaluated: {decisionKey}");
                }
                else if (_dmnEngine != null && _registeredDecisions.TryGetValue(decisionKey, out var decision))
                {
                    var result = _dmnEngine
                        .EvaluateDecisionAsync(decision, new Dictionary<string, object>(varsRef), CancellationToken.None)
                        .GetAwaiter().GetResult();

                    foreach (var kv in result)
                        varsRef[kv.Key] = kv.Value;
                    trace.Add($"DecisionEvaluated: {decisionKey} (local)");
                }
                else
                {
                    if (!TryApplyExplicitSimulation(task, varsRef, trace))
                        TryAutoInferVariableForGateway(task, model, varsRef, trace);
                }
            }
            catch (Exception ex)
            {
                trace.Add($"DecisionError: {task.Id} {ex.Message}");
            }
        }

        // Step 4: apply zeebe:ioMapping consistently (writes into same varsRef)
        ApplyZeebeIoMapping(task, varsRef, trace);

        // Multi-instance heuristic
        if (task.Attributes != null && task.Attributes.Keys.Any(k => k.Contains("multiInstance", StringComparison.OrdinalIgnoreCase)))
        {
            var card = task.Attributes.FirstOrDefault(kv => kv.Key.Contains("cardinality", StringComparison.OrdinalIgnoreCase)).Value;
            if (!int.TryParse(card, out var c) || c <= 0) c = 1;
            for (int i = 0; i < c; i++) trace.Add($"MIInstance: {task.Id} #{i + 1}");
        }

        foreach (var flow in model.SequenceFlows.Where(f => f.SourceRef == task.Id))
            EmitNewToken(trace, tokenId, parentTxn, flow);

        if (_tokens.TryGetValue(tokenId, out var tk))
            _tokens[tokenId] = tk with { Active = false };
    }

    private static void ExecuteDefaultServiceTask(BpmnTask task, IDictionary<string, object> variables, List<string> trace)
    {
        trace.Add($"ExecutingDefaultHandler: {task.Id}");

        if (task.Attributes != null)
        {
            foreach (var attribute in task.Attributes)
                trace.Add($"Attribute: {attribute.Key} = {attribute.Value}");
        }

        var resultVariable = task.Attributes?.GetValueOrDefault("resultVariable") ?? $"{task.Id}_Result";
        var result = $"Default result for task {task.Id}";
        variables[resultVariable] = result;
        trace.Add($"DefaultResultSet: {task.Id} => {result}");
    }

    // Add near other private helpers (bottom helper region)

    private IDictionary<string, object> GetOrCreateWorkingVariables(BpmnModel model)
    {
        // Execute() initializes this per process instance. The fallback only protects
        // internal callers that access variables before execution initialization.
        return _workingVariables ??= new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
    }

    private bool TryApplyExplicitSimulation(BpmnTask task, IDictionary<string, object> vars, List<string> trace)
    {
        if (task.Attributes == null || task.Attributes.Count == 0) return false;
        var applied = false;

        // Pattern 1: sim:VariableName => value
        foreach (var (k, v) in task.Attributes)
        {
            if (k.StartsWith("sim:", StringComparison.OrdinalIgnoreCase))
            {
                var varName = k[4..].Trim();
                if (varName.Length == 0) continue;
                vars[varName] = v;
                trace.Add($"DecisionSimulated: {task.Id} {varName}='{v}' (explicit)");
                applied = true;
            }
        }

        // Pattern 2: simulation:output (JSON object)
        if (task.Attributes.TryGetValue("simulation:output", out var json) && json is string s && s.Contains('{'))
        {
            try
            {
                var map = JsonSerializer.Deserialize<Dictionary<string, object>>(s);
                if (map != null)
                {
                    foreach (var (k, val) in map)
                    {
                        vars[k] = val!;
                        trace.Add($"DecisionSimulated: {task.Id} {k}='{val}' (json)");
                    }
                    applied = true;
                }
            }
            catch
            {
                trace.Add($"DecisionSimulationParseError: {task.Id}");
            }
        }

        return applied;
    }

    private bool TryAutoInferVariableForGateway(BpmnTask task, BpmnModel model, IDictionary<string, object> vars, List<string> trace)
    {
        // Look at single outgoing flow -> exclusive gateway -> its conditional flows
        var directFlows = model.SequenceFlows.Where(f => f.SourceRef == task.Id).ToList();
        if (directFlows.Count != 1) return false;

        var potentialGatewayId = directFlows[0].TargetRef;
        var gateway = model.Gateways.FirstOrDefault(g => g.Id == potentialGatewayId && g.Type == "exclusiveGateway");
        if (gateway == null) return false;

        var gwFlows = model.SequenceFlows.Where(f => f.SourceRef == gateway.Id).ToList();
        foreach (var flow in gwFlows)
        {
            var cond = GetConditionExpression(flow);
            if (string.IsNullOrWhiteSpace(cond)) continue;
            if (ParseSimpleEquality(cond!, out var varName, out var literal))
            {
                if (!vars.ContainsKey(varName))
                {
                    vars[varName] = literal;
                    trace.Add($"DecisionSimulated:auto {varName}='{literal}' for gateway {gateway.Id}");
                    return true;
                }
            }
        }
        return false;
    }

    private static string? GetConditionExpression(BpmnSequenceFlow flow)
    {
        try { return flow.GetType().GetProperty("ConditionExpression")?.GetValue(flow)?.ToString(); }
        catch { return null; }
    }

    private static bool ParseSimpleEquality(string raw, out string varName, out string literal)
    {
        varName = string.Empty;
        literal = string.Empty;
        if (string.IsNullOrWhiteSpace(raw)) return false;

        var expr = raw.Trim();
        if (expr.StartsWith("${") && expr.EndsWith("}"))
            expr = expr[2..^1].Trim();

        string[] ops = ["==", "!="];
        foreach (var op in ops)
        {
            var idx = expr.IndexOf(op, StringComparison.Ordinal);
            if (idx > 0)
            {
                var left = expr[..idx].Trim();
                var right = expr[(idx + op.Length)..].Trim();
                right = TrimQuotes(right);
                if (left.Length > 0 && right.Length > 0)
                {
                    varName = left;
                    literal = right;
                    return true;
                }
            }
        }

        return false;
    }

    private static string TrimQuotes(string s)
    {
        if (s.Length >= 2 && ((s[0] == '"' && s[^1] == '"') || (s[0] == '\'' && s[^1] == '\'')))
            return s[1..^1];
        return s;
    }
    // MIWG-compliant join approximation using JoinContext
    // FIX: Gibt jetzt zusätzlich zurück, ob der Join GERADE JETZT (mit diesem Token) zum
    // ersten Mal erfüllt wurde ("justFired"), statt nur ob er (irgendwann, auch schon
    // früher) erfüllt ist. Vorher führte JEDE weitere Ankunft nach dem ersten Feuern
    // dazu, dass der Aufrufer das Gateway erneut komplett aufspalten ließ
    // (HandleGatewayForToken -> neue Tokens auf allen ausgehenden Flows) – bei
    // rückläufigen Strukturen (z. B. Retry-/Konsultations-Schleifen) führte das zu
    // exponentieller Token-Vervielfachung bis zum VisitLimitReached-Abbruch.
    private bool RegisterParallelJoinArrivalCompliant(string gatewayId, string? incomingFlowId, List<string> trace, out bool justFired)
    {
        justFired = false;
        if (!_incomingByTarget.TryGetValue(gatewayId, out var incomings)) return true; // nothing to wait for
        if (!_joinContexts.TryGetValue(gatewayId, out var ctx))
        {
            ctx = new JoinContext();
            foreach (var f in incomings) ctx.RequiredFlows.Add(f.Id); // all flows required for parallel join
            _joinContexts[gatewayId] = ctx;
            trace.Add($"JoinInit: {gatewayId} required={string.Join(',', ctx.RequiredFlows)} (parallel)");
        }
        if (!string.IsNullOrEmpty(incomingFlowId)) ctx.ArrivedFlows.Add(incomingFlowId);
        trace.Add($"JoinProgress: {gatewayId} {ctx.ArrivedFlows.Count}/{ctx.RequiredFlows.Count}");
        if (!ctx.Fired && ctx.ArrivedFlows.IsSupersetOf(ctx.RequiredFlows))
        {
            ctx.Fired = true;
            justFired = true;
            trace.Add($"JoinSatisfied: {gatewayId}");
            return true;
        }
        if (ctx.Fired)
        {
            // Überzähliger/verspäteter Token nach bereits erfolgtem Join: still konsumieren,
            // NICHT erneut aufspalten lassen.
            trace.Add($"JoinExcessTokenConsumed: {gatewayId} Token-Flow {incomingFlowId}");
        }
        return ctx.Fired;
    }

    private bool RegisterInclusiveJoinArrivalCompliant(string gatewayId, string? incomingFlowId, List<string> trace, out bool justFired)
    {
        justFired = false;
        if (!_incomingByTarget.TryGetValue(gatewayId, out var incomings)) return true;
        if (!_joinContexts.TryGetValue(gatewayId, out var ctx))
        {
            ctx = new JoinContext();
            // Determine required flows heuristically: those whose source has been visited/pending or already produced a token
            foreach (var f in incomings)
            {
                if (!_disabledFlows.Contains(f.Id)) ctx.RequiredFlows.Add(f.Id);
            }
            _joinContexts[gatewayId] = ctx;
            trace.Add($"JoinInit: {gatewayId} required={string.Join(',', ctx.RequiredFlows)} (inclusive)");
        }
        if (!string.IsNullOrEmpty(incomingFlowId)) ctx.ArrivedFlows.Add(incomingFlowId);
        trace.Add($"JoinProgress: {gatewayId} {ctx.ArrivedFlows.Count}/{ctx.RequiredFlows.Count}");
        // inclusive: if all required arrived OR all remaining required flows are disabled/unreachable
        if (!ctx.Fired && ctx.ArrivedFlows.IsSupersetOf(ctx.RequiredFlows))
        {
            ctx.Fired = true;
            justFired = true;
            trace.Add($"JoinSatisfied: {gatewayId}");
            return true;
        }
        if (ctx.Fired)
        {
            trace.Add($"JoinExcessTokenConsumed: {gatewayId} Token-Flow {incomingFlowId}");
        }
        return ctx.Fired;
    }

    // Existing helper methods below (ProcessBoundaryEvents, events, reflection etc.) remain unchanged.
}

// === Helper & Legacy Compatibility Methods (re-added for token engine) ===
partial class ProcessEngine
{
    private void PrecomputeIncoming(BpmnModel model)
    {
        _incomingByTarget.Clear();
        foreach (var f in model.SequenceFlows)
        {
            if (!_incomingByTarget.TryGetValue(f.TargetRef, out var list))
            {
                list = new List<BpmnSequenceFlow>();
                _incomingByTarget[f.TargetRef] = list;
            }
            list.Add(f);
        }
    }

    private void IndexEventSubprocessStartEvents(BpmnModel model, List<string> trace)
    {
        _eventSubprocessStartEvents.Clear();
        foreach (var sp in model.Subprocesses)
        {
            if (!IsEventSubprocess(sp)) continue;
            var starts = model.Events.Where(e =>
                e.Type == "startEvent"
                && string.Equals(e.SubprocessId, sp.Id, StringComparison.Ordinal)).ToList();
            foreach (var ev in starts)
            {
                var evtType = GetEventDefinitionType(ev) ?? "message";
                if (!_eventSubprocessStartEvents.TryGetValue(evtType, out var list))
                {
                    list = new List<string>();
                    _eventSubprocessStartEvents[evtType] = list;
                }
                list.Add(ev.Id);
                trace.Add($"IndexedEventSubprocessStart: {ev.Id} ({evtType}) for {sp.Id}");
            }
        }
    }

    private static IReadOnlyDictionary<string, string> BuildLinkCatchMap(IEnumerable<BpmnEvent> events)
    {
        var dict = new Dictionary<string, string>();
        foreach (var e in events)
        {
            if (e.Type.Contains("Catch", StringComparison.OrdinalIgnoreCase) &&
                (HasDefinition(e, "link") || GetEventDefinitionType(e) == "link"))
            {
                var name = GetLinkName(e);
                if (!string.IsNullOrEmpty(name)) dict[name!] = e.Id;
            }
        }
        return dict;
    }

    private bool IsParallelJoin(BpmnGateway gateway)
        => gateway.Type == "parallelGateway" && _incomingByTarget.TryGetValue(gateway.Id, out var incoming) && incoming.Count > 1;
    private bool IsInclusiveJoin(BpmnGateway gateway)
        => gateway.Type == "inclusiveGateway" && _incomingByTarget.TryGetValue(gateway.Id, out var incoming) && incoming.Count > 1;

    private void HandleSubprocess(BpmnSubprocess sp, BpmnModel model, List<string> trace,
        Queue<(string TokenId, string NodeId, string? FromFlow, string? ParentTxn)> queue, string? parentTxn)
    {
        trace.Add($"SubProcess: {sp.Id}");
        var flow = model.SequenceFlows.FirstOrDefault(f => f.SourceRef == sp.Id);
        if (flow != null)
        {
            EmitNewToken(trace, "SP", parentTxn, flow); // source token label simplified
        }
    }

    private void HandleIntermediateEvent(BpmnEvent evt, List<string> trace,
        Queue<(string TokenId, string NodeId, string? FromFlow, string? ParentTxn)> queue,
        BpmnModel model, IReadOnlyDictionary<string, string> linkCatchMap, string? parentTxn)
    {
        trace.Add($"{evt.Type}: {evt.Id}");
        var evtDefType = GetEventDefinitionType(evt);
        var isThrow = evt.Type.Contains("Throw", StringComparison.OrdinalIgnoreCase);

        var hasLink = evtDefType == "link" || HasDefinition(evt, "link");
        if (hasLink)
        {
            var linkName = GetLinkName(evt);
            if (!string.IsNullOrEmpty(linkName) && isThrow)
            {
                if (linkCatchMap.TryGetValue(linkName!, out var target))
                {
                    var newTokenId = NextTokenId();
                    _tokens[newTokenId] = new Token(newTokenId, target, parentTxn, true);
                    queue.Enqueue((newTokenId, target, null, parentTxn));
                    trace.Add($"LinkThrow: {evt.Id}->{target} [Token {newTokenId}]");
                }
                else trace.Add($"LinkThrowUnresolved: {evt.Id} ({linkName})");
            }
        }

        // NEU: Bei Message-/Escalation-/Signal-Throw-Events den jeweiligen "last thrown"-
        // Tracker setzen, damit ProcessBoundaryEvents weiter unten tatsächlich weiß, dass
        // (und ggf. mit welcher Referenz) der Trigger aufgetreten ist – statt Boundary
        // Events bedingungslos beim Erreichen der Aktivität feuern zu lassen.
        if (isThrow)
        {
            switch (evtDefType)
            {
                case "message":
                    _lastMessageRef = GetDefinitionRefValue(evt, "message") ?? "__message";
                    trace.Add($"MessageThrown: {_lastMessageRef} at {evt.Id}");
                    break;
                case "escalation":
                    _lastEscalationCode = GetDefinitionRefValue(evt, "escalation") ?? "__escalation";
                    trace.Add($"EscalationThrown: {_lastEscalationCode} at {evt.Id}");
                    break;
                case "signal":
                    _lastSignalRef = GetDefinitionRefValue(evt, "signal") ?? "__signal";
                    trace.Add($"SignalThrown: {_lastSignalRef} at {evt.Id}");
                    break;
            }
        }

        foreach (var f in model.SequenceFlows.Where(f => f.SourceRef == evt.Id))
        {
            EmitNewToken(trace, "EVT", parentTxn, f);
        }
    }

    // ===== Modify ProcessBoundaryEvents to implement step 5 (filtered error boundary firing) =====
    private bool ProcessBoundaryEvents(string activityId, BpmnModel model, List<string> trace,
        Queue<(string TokenId, string NodeId, string? FromFlow, string? ParentTxn)> queue, string? parentTxn)
    {
        var boundaryEvents = model.Events
            .Where(e => e.Type == "boundaryEvent" && GetAttachedToRef(e) == activityId &&
                        !IsCompensationBoundary(e))
            .ToList();

        if (boundaryEvents.Count == 0) return false;

        var interruptingTriggered = false;
        foreach (var b in boundaryEvents)
        {
            var defType = GetEventDefinitionType(b) ?? string.Empty;

            // FIX: Bisher hatte nur "error" eine echte Trigger-Bedingung. Alle anderen Typen
            // (message, escalation, signal) feuerten bedingungslos beim bloßen Erreichen der
            // Aktivität – dadurch war z. B. der "Happy Path" eines Subprocess mit
            // angehängtem message-/escalation-Boundary-Event de facto unerreichbar.
            // "timer" und "conditional" bleiben bewusst unangetastet (siehe Hinweis unten) –
            // dafür fehlt der Engine aktuell ein echtes Zeit-/Bedingungsmodell.
            if (defType.Equals("error", StringComparison.OrdinalIgnoreCase))
            {
                var errorCodeProp = b.GetType().GetProperty("ErrorCode")?.GetValue(b)?.ToString();
                if (string.IsNullOrWhiteSpace(_lastErrorCode) ||
                    (!string.IsNullOrWhiteSpace(errorCodeProp) &&
                     !string.Equals(errorCodeProp, _lastErrorCode, StringComparison.OrdinalIgnoreCase)))
                {
                    trace.Add($"BoundaryEventSkipped: {b.Id} error(no-match) on {activityId}");
                    continue;
                }
            }
            else if (defType.Equals("message", StringComparison.OrdinalIgnoreCase))
            {
                var msgRefProp = GetDefinitionRefValue(b, "message");
                if (string.IsNullOrWhiteSpace(_lastMessageRef) ||
                    (!string.IsNullOrWhiteSpace(msgRefProp) &&
                     !string.Equals(msgRefProp, _lastMessageRef, StringComparison.OrdinalIgnoreCase)))
                {
                    trace.Add($"BoundaryEventSkipped: {b.Id} message(no-match) on {activityId}");
                    continue;
                }
            }
            else if (defType.Equals("escalation", StringComparison.OrdinalIgnoreCase))
            {
                var escCodeProp = GetDefinitionRefValue(b, "escalation");
                if (string.IsNullOrWhiteSpace(_lastEscalationCode) ||
                    (!string.IsNullOrWhiteSpace(escCodeProp) &&
                     !string.Equals(escCodeProp, _lastEscalationCode, StringComparison.OrdinalIgnoreCase)))
                {
                    trace.Add($"BoundaryEventSkipped: {b.Id} escalation(no-match) on {activityId}");
                    continue;
                }
            }
            else if (defType.Equals("signal", StringComparison.OrdinalIgnoreCase))
            {
                var sigRefProp = GetDefinitionRefValue(b, "signal");
                if (string.IsNullOrWhiteSpace(_lastSignalRef) ||
                    (!string.IsNullOrWhiteSpace(sigRefProp) &&
                     !string.Equals(sigRefProp, _lastSignalRef, StringComparison.OrdinalIgnoreCase)))
                {
                    trace.Add($"BoundaryEventSkipped: {b.Id} signal(no-match) on {activityId}");
                    continue;
                }
            }
            // "timer" / "conditional" (und alles sonst Unbekannte): bewusst weiterhin
            // unbedingt feuernd – siehe Erläuterung unten im Antworttext.

            var isInterrupting = IsInterruptingBoundary(b);
            trace.Add($"BoundaryEvent: {b.Id} {(isInterrupting ? "interrupting" : "nonInterrupting")} {defType} on {activityId}");
            foreach (var f in model.SequenceFlows.Where(f => f.SourceRef == b.Id))
            {
                EmitNewToken(trace, "BOUND", parentTxn, f);
            }
            if (isInterrupting) interruptingTriggered = true;
        }
        return interruptingTriggered;
    }

    // Reflection helpers reused
    private static bool HasDefinition(BpmnEvent evt, string kind)
    {
        try
        {
            var prop = evt.GetType().GetProperty("Definitions");
            if (prop?.GetValue(evt) is IEnumerable defs)
                foreach (var d in defs)
                {
                    var k = d.GetType().GetProperty("Kind")?.GetValue(d)?.ToString();
                    if (k == kind) return true;
                }
        }
        catch { }
        return false;
    }
    private static string? GetLinkName(BpmnEvent evt)
    {
        try
        {
            var prop = evt.GetType().GetProperty("Definitions");
            if (prop?.GetValue(evt) is IEnumerable defs)
                foreach (var d in defs)
                {
                    var kind = d.GetType().GetProperty("Kind")?.GetValue(d)?.ToString();
                    if (kind == "link")
                        return d.GetType().GetProperty("Name")?.GetValue(d)?.ToString();
                }
        }
        catch { }
        return null;
    }

    // NEU: Generischer Helper, um die Referenz/Namen einer Message-/Signal-/Escalation-
    // Definition eines Events auszulesen. Sucht in den "Definitions" nach dem passenden
    // "Kind" und liest dann "Name" oder alternativ "Ref"/"Code" (je nachdem, was am
    // konkreten Definitions-DTO vorhanden ist).
    // ACHTUNG: Die genauen Property-Namen ("Name" vs. "Ref" vs. "Code") sind hier nur
    // best-effort geraten und müssen gegen eure tatsächlichen Definitions-DTOs
    // (z. B. MessageEventDefinitionDto, SignalEventDefinitionDto, EscalationEventDefinitionDto)
    // verifiziert werden.
    private static string? GetDefinitionRefValue(BpmnEvent evt, string kind)
    {
        try
        {
            var prop = evt.GetType().GetProperty("Definitions");
            if (prop?.GetValue(evt) is IEnumerable defs)
                foreach (var d in defs)
                {
                    var k = d.GetType().GetProperty("Kind")?.GetValue(d)?.ToString();
                    if (k != kind) continue;
                    var dType = d.GetType();
                    var value = dType.GetProperty("Name")?.GetValue(d)?.ToString()
                                ?? dType.GetProperty("Ref")?.GetValue(d)?.ToString()
                                ?? dType.GetProperty("Code")?.GetValue(d)?.ToString();
                    return value;
                }
        }
        catch { }
        return null;
    }
    private static string? GetEventDefinitionType(BpmnEvent e)
    {
        try { return e.GetType().GetProperty("EventDefinitionType")?.GetValue(e)?.ToString(); } catch { return null; }
    }
    private static string? GetAttachedToRef(BpmnEvent e)
    {
        try { return e.GetType().GetProperty("AttachedToRef")?.GetValue(e)?.ToString(); } catch { return null; }
    }
    private static bool IsInterruptingBoundary(BpmnEvent e)
    {
        try { var v = e.GetType().GetProperty("CancelActivity")?.GetValue(e); if (v is bool b) return b; } catch { }
        return true;
    }
    private static bool IsEventSubprocess(BpmnSubprocess sp)
    {
        try { var v = sp.GetType().GetProperty("IsEventSubprocess")?.GetValue(sp); if (v is bool b) return b; } catch { }
        return false;
    }
    private static bool IsTransactionSubprocess(BpmnSubprocess sp)
    {
        try { var v = sp.GetType().GetProperty("IsTransaction")?.GetValue(sp); if (v is bool b) return b; } catch { }
        return false;
    }

    // ===== Add helper methods for zeebe:ioMapping & expression (step 4) and small FEEL-lite evaluation (step 6 placeholder) =====
    private void ApplyZeebeIoMapping(BpmnTask task, IDictionary<string, object> vars, List<string> trace)
    {
        if (task.Attributes == null) return;
        if (!task.Attributes.TryGetValue("zeebe:ioMapping", out var raw) || string.IsNullOrWhiteSpace(raw))
            return;

        try
        {
            // Expect JSON: { "targetVar": "literal or =expression", ... }
            var map = JsonSerializer.Deserialize<Dictionary<string, string>>(raw);
            if (map == null || map.Count == 0) return;

            foreach (var (target, source) in map)
            {
                if (string.IsNullOrWhiteSpace(target)) continue;
                if (string.IsNullOrWhiteSpace(source))
                {
                    vars[target] = null!;
                    continue;
                }

                if (source.StartsWith("="))
                {
                    vars[target] = EvaluateSimpleExpression(source[1..].Trim(), vars);
                    trace.Add($"ZeebeIOMappingExpr: {task.Id} {target}='{vars[target]}'");
                }
                else
                {
                    vars[target] = source;
                    trace.Add($"ZeebeIOMappingSet: {task.Id} {target}='{source}'");
                }
            }
            trace.Add($"ZeebeIOMappingApplied: {task.Id}");
        }
        catch (Exception ex)
        {
            trace.Add($"ZeebeIOMappingError: {task.Id} {ex.Message}");
        }
    }

    // Step 6: minimal evaluator: variable reference or string/number literal, simple concatenation with +
    private object EvaluateSimpleExpression(string expr, IDictionary<string, object> vars)
    {
        if (string.IsNullOrWhiteSpace(expr)) return string.Empty;

        // Split by + (no precedence handling)
        var parts = expr.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 1)
            return ResolveAtom(parts[0]);

        var sb = new System.Text.StringBuilder();
        foreach (var part in parts)
        {
            sb.Append(ResolveAtom(part));
        }
        return sb.ToString();

        object ResolveAtom(string atom)
        {
            // strip quotes
            atom = atom.Trim();
            if ((atom.StartsWith("\"") && atom.EndsWith("\"")) ||
                (atom.StartsWith("'") && atom.EndsWith("'")))
                return atom[1..^1];

            // numeric?
            if (int.TryParse(atom, out var i)) return i;
            if (double.TryParse(atom, out var d)) return d;

            // variable lookup
            if (vars.TryGetValue(atom, out var val)) return val ?? string.Empty;

            return atom; // fallback literal
        }
    }
    private static bool IsCompensationBoundary(BpmnEvent evt)
    {
        var type = GetEventDefinitionType(evt);

        return type?.Contains(
                   "compensate",
                   StringComparison.OrdinalIgnoreCase) == true
               || HasDefinition(evt, "compensate");
    }
}
