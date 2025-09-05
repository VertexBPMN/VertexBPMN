using Jint;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using VertexBPMN.Core.Bpmn;
using VertexBPMN.Core.Cmmn;
using VertexBPMN.Core.Domain;
using VertexBPMN.Core.Exceptions;
using VertexBPMN.Core.Messaging;
using VertexBPMN.Core.Scripting;
using VertexBPMN.Core.Services;

namespace VertexBPMN.Core.Engine
{
    public class DistributedTokenEngine : IDistributedTokenEngine, IDisposable
    {
        private readonly ILogger<DistributedTokenEngine> _logger;
        private readonly ServiceTaskRegistry _serviceRegistry;
        private readonly IMessageDispatcher _messageDispatcher;
        private readonly IProcessInstanceStore _store;
        private readonly IDmnEngine _dmnEngine;
        private readonly IDmnParser _dmnParser;
        private readonly ICmmnParser _cmmnParser;
        private readonly IBpmnParser _bpmnParser;
        private readonly IAiDecisionService _aiDecisionService;
        private readonly Tracer _tracer;
        private readonly ConcurrentDictionary<Guid, CaseToken> _processingCaseTokens = new();
        private readonly ConcurrentDictionary<Guid, ExecutionToken> _processingTokens = new();
        private readonly ConcurrentDictionary<string, Jint.Engine> _jintCache = new(); // Jint-Cache für Performance
        private readonly Timer _heartbeatTimer;

        public DistributedTokenEngine(
            ILogger<DistributedTokenEngine> logger,
            ServiceTaskRegistry serviceRegistry,
            IMessageDispatcher dispatcher,
            IProcessInstanceStore store,
            IDmnEngine dmnEngine,
            IDmnParser dmnParser,
            ICmmnParser cmmnParser,
            IBpmnParser bpmnParser,
            IAiDecisionService aiDecisionService,
            TracerProvider tracerProvider)
        {
             _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _serviceRegistry = serviceRegistry ?? throw new ArgumentNullException(nameof(serviceRegistry));
            _messageDispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _dmnEngine = dmnEngine ?? throw new ArgumentNullException(nameof(dmnEngine));
            _dmnParser = dmnParser ?? throw new ArgumentNullException(nameof(dmnParser));
            _cmmnParser = cmmnParser ?? throw new ArgumentNullException(nameof(cmmnParser));
            _bpmnParser = bpmnParser ?? throw new ArgumentNullException(nameof(bpmnParser));
            _aiDecisionService = aiDecisionService ?? throw new ArgumentNullException(nameof(aiDecisionService));
            _tracer = tracerProvider.GetTracer("VertexBPMN");

            var currentWorker = new WorkerNode(
                Environment.MachineName,
                Environment.MachineName,
                5000,
                DateTime.UtcNow,
                ["userTask", "serviceTask", "mcpServiceTask", "scriptTask", "businessRuleTask", "subprocess", "humanTask", "caseTask", "adHocSubprocess", "eventListener"],
                0,
                10,
                SupportsDmn: true,
                SupportsCmmn: true
            );
            _store.SaveWorkerAsync(currentWorker).GetAwaiter().GetResult();

            _heartbeatTimer = new Timer(ProcessHeartbeatsAsync, null, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));
        }


        public async Task<List<string>> ExecuteAsync(BpmnModel model, CancellationToken cancellationToken = default)
        {

            var trace = new List<string>();
            var processInstanceId = Guid.NewGuid();
            try
            {

                var startEvent = model.Events.FirstOrDefault(e => e.Type == "startEvent") ??
                                 throw new BpmnEngineException("No startEvent found");
                trace.Add($"DistributedExecution: Starting process: {processInstanceId}, StartEvent: {startEvent.Id}");

                var nextIds = model.SequenceFlows.Where(f => f.SourceRef == startEvent.Id).Select(f => f.TargetRef).ToList();
                foreach (var id in nextIds)
                {
                    var token = new ExecutionToken(Guid.NewGuid(), processInstanceId, id, "start", new Dictionary<string, object>(model.ProcessVariables ?? new()), DateTime.UtcNow);
                    await DistributeTokenAsync(token, cancellationToken);
                    trace.Add($"Start->Token:{id}");
                    await ProcessDistributedTokensAsync(model, trace, cancellationToken);
                }
                return trace;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to execute process {ProcessId}", model.Id);
                trace.Add($"ExecutionError: {ex.Message}");
                throw new DistributedTokenException($"Failed to execute process {model.Id}", ex);
            }
        }

        public async Task<List<string>> ExecuteCaseAsync(CaseModel model, CancellationToken cancellationToken = default)
        {
            var trace = new List<string>();
            var caseInstanceId = Guid.NewGuid();

            try
            {
                trace.Add($"DistributedCaseExecution: Starting case {caseInstanceId}");
                var initialCaseFile = model.CaseFileItems.ToDictionary(item => item.Id, item => item.Value ?? new object());

                foreach (var planItem in model.PlanItems.Where(pi => pi.EntrySentryRefs == null || pi.EntrySentryRefs.Count == 0))
                {
                    var token = new CaseToken(
                        Guid.NewGuid(),
                        caseInstanceId,
                        planItem.Id,
                        planItem.Type,
                        initialCaseFile,
                        DateTime.UtcNow
                    );
                    await DistributeCaseTokenAsync(token, cancellationToken);
                    trace.Add($"CaseTokenDistributed: {token.Id} -> {token.CurrentPlanItemId}");
                }

                await ProcessDistributedCaseTokensAsync(model, trace, cancellationToken);
                return trace;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to execute case {CaseId}", model.Id);
                trace.Add($"CaseExecutionError: {ex.Message}");
                throw new DistributedTokenException($"Failed to execute case {model.Id}", ex);
            }
        }


        /// <summary>
        /// Prüft, ob ein Knoten ausgeführt werden kann.
        /// </summary>
        public async Task<bool> CanExecuteAsync(string nodeId, CancellationToken cancellationToken = default)
        {
            var workers = await _store.GetActiveWorkersAsync();
            return workers.Any(w => w.CurrentLoad < w.MaxCapacity && DateTime.UtcNow - w.LastHeartbeat < TimeSpan.FromMinutes(2));
        }

        public async Task DistributeTokenAsync(ExecutionToken token, CancellationToken cancellationToken = default)
        {
            try
            {
                var bestWorker = await FindBestWorkerAsync(token.NodeType);
                var assignedToken = token with
                {
                    AssignedWorker = bestWorker?.Id,
                    AssignedAt = DateTime.UtcNow
                };

                await _store.SaveTokenAsync(assignedToken);
                await _messageDispatcher.PublishTokenAsync(assignedToken, cancellationToken);
                _logger.LogInformation("Token {TokenId} assigned to worker {WorkerId}", token.Id, bestWorker?.Id ?? "none");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to distribute token {TokenId}", token.Id);
                throw new DistributedTokenException($"Failed to distribute token {token.Id}", ex);
            }
   
        }


        public async Task DistributeCaseTokenAsync(CaseToken token, CancellationToken cancellationToken = default)
        {
            try
            {
                var bestWorker = await FindBestWorkerAsync(token.PlanItemType);
                var assignedToken = token with
                {
                    AssignedWorker = bestWorker?.Id,
                    AssignedAt = DateTime.UtcNow
                };

                await _store.SaveCaseTokenAsync(assignedToken);
                await _messageDispatcher.PublishCaseTokenAsync(assignedToken, cancellationToken);
                _logger.LogInformation("CaseToken {TokenId} assigned to worker {WorkerId}", token.Id, bestWorker?.Id ?? "none");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to distribute case token {TokenId}", token.Id);
                throw new DistributedTokenException($"Failed to distribute case token {token.Id}", ex);
            }
        }

        /// <summary>
        /// Ruft alle ausstehenden Tokens ab.
        /// </summary>
        public async Task<List<ExecutionToken>> GetPendingTokensAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                return await _store.GetPendingTokensAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve pending tokens");
                throw new DistributedTokenException("Failed to retrieve pending tokens", ex);
            }
        }

        public async Task<List<CaseToken>> GetPendingCaseTokensAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                return await _store.GetPendingCaseTokensAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve pending case tokens");
                throw new DistributedTokenException("Failed to retrieve pending case tokens", ex);
            }
        }
        /// <summary>
        /// Registriert einen neuen Worker.
        /// </summary>
        public async Task RegisterWorkerAsync(WorkerNode worker)
        {
            try
            {
                await _store.SaveWorkerAsync(worker);
                _logger.LogInformation("Registered worker {WorkerId} with capacity {Capacity}", worker.Id, worker.MaxCapacity);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to register worker {WorkerId}", worker.Id);
                throw new DistributedTokenException($"Failed to register worker {worker.Id}", ex);
            }
        }
        /// <summary>
        /// Entfernt einen Worker.
        /// </summary>
        public async Task UnregisterWorkerAsync(string workerId)
        {
            try
            {
                await _store.RemoveWorkerAsync(workerId);
                _logger.LogInformation("Unregistered worker {WorkerId}", workerId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to unregister worker {WorkerId}", workerId);
                throw new DistributedTokenException($"Failed to unregister worker {workerId}", ex);
            }
        }
        /// <summary>
        /// Aktualisiert den Heartbeat eines Workers.
        /// </summary>
        public async Task UpdateWorkerHeartbeatAsync(string workerId)
        {
            try
            {
                var worker = await _store.GetWorkerAsync(workerId);
                if (worker != null)
                {
                    await _store.SaveWorkerAsync(worker with { LastHeartbeat = DateTime.UtcNow });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update heartbeat for worker {WorkerId}", workerId);
            }
        }
        // <summary>
        /// Registriert ein DMN-Modell.
        /// </summary>
        public async Task RegisterDmnModelAsync(string decisionId, string dmnXml)
        {
            try
            {
                await _dmnParser.ParseAsync(dmnXml);
                await _store.SaveDmnModelAsync(decisionId, dmnXml);
                _logger.LogInformation("Registered DMN model {DecisionId}", decisionId);
            }
            catch (DmnParseException ex)
            {
                _logger.LogError(ex, "Invalid DMN XML for decision {DecisionId}", decisionId);
                throw new DistributedTokenException($"Invalid DMN XML for decision {decisionId}", ex);
            }
        }

        public async Task RegisterCmmnModelAsync(string caseId, string cmmnXml)
        {
            try
            {
                await _cmmnParser.ParseAsync(cmmnXml);
                await _store.SaveCmmnModelAsync(caseId, cmmnXml);
                _logger.LogInformation("Registered CMMN model {CaseId}", caseId);
            }
            catch (CmmnParseException ex)
            {
                _logger.LogError(ex, "Invalid CMMN XML for case {CaseId}", caseId);
                throw new DistributedTokenException($"Invalid CMMN XML for case {caseId}", ex);
            }
        }

        public async Task AddDiscretionaryItemAsync(string caseId, PlanItem planItem, CancellationToken cancellationToken = default)
        {
            try
            {
                if (!planItem.IsDiscretionary)
                    throw new DistributedTokenException("PlanItem must be discretionary");

                var cmmnXml = await _store.GetCmmnModelAsync(caseId)
                    ?? throw new DistributedTokenException($"CMMN model {caseId} not found");
                var caseModel = await _cmmnParser.ParseAsync(cmmnXml);

                var updatedPlanItems = new List<PlanItem>(caseModel.PlanItems) { planItem };
                var updatedModel = caseModel with { PlanItems = updatedPlanItems };
                await _store.UpdateCaseModelAsync(updatedModel);

                var caseTokens = await _store.GetPendingCaseTokensAsync();
                var caseToken = caseTokens.FirstOrDefault(t => t.CaseInstanceId == Guid.Parse(caseId));
                if (caseToken.Id != default)
                {
                    var newToken = new CaseToken(
                        Guid.NewGuid(),
                        caseToken.CaseInstanceId,
                        planItem.Id,
                        planItem.Type,
                        new Dictionary<string, object>(caseToken.CaseFile),
                        DateTime.UtcNow
                    );
                    await DistributeCaseTokenAsync(newToken, cancellationToken);
                    _logger.LogInformation("Discretionary item {PlanItemId} added to case {CaseId}", planItem.Id, caseId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to add discretionary item to case {CaseId}", caseId);
                throw new DistributedTokenException($"Failed to add discretionary item to case {caseId}", ex);
            }
        }

        public async Task UpdateCaseFileItemAsync(string caseId, string caseFileItemId, object newValue, CancellationToken cancellationToken = default)
        {
            using var span = _tracer.StartActiveSpan("UpdateCaseFileItem");
            span.SetAttribute("caseId", caseId);
            span.SetAttribute("caseFileItemId", caseFileItemId);
            try
            {
                var cmmnXml = await _store.GetCmmnModelAsync(caseId)
                    ?? throw new DistributedTokenException($"CMMN model {caseId} not found");
                var caseModel = await _cmmnParser.ParseAsync(cmmnXml);

                var caseFileItem = caseModel.CaseFileItems.FirstOrDefault(cfi => cfi.Id == caseFileItemId)
                    ?? throw new DistributedTokenException($"CaseFileItem {caseFileItemId} not found");
                var updatedCaseFileItems = caseModel.CaseFileItems
                    .Select(cfi => cfi.Id == caseFileItemId ? cfi with { Value = newValue } : cfi)
                    .ToList();
                var updatedModel = caseModel with { CaseFileItems = updatedCaseFileItems };
                await _store.UpdateCaseModelAsync(updatedModel);

                var updateEvent = new CaseFileUpdateEvent(caseId, caseFileItemId, newValue, DateTime.UtcNow);
                await _messageDispatcher.PublishCaseFileUpdateAsync(updateEvent, cancellationToken);
                _logger.LogInformation("CaseFileItem {CaseFileItemId} updated in case {CaseId}", caseFileItemId, caseId);
                span.SetStatus(Status.Ok);

                // Trigger EventListener für Case File Updates
                var caseTokens = await _store.GetPendingCaseTokensAsync();
                var relevantTokens = caseTokens.Where(t => t.CaseInstanceId == Guid.Parse(caseId)).ToList();
                foreach (var token in relevantTokens)
                {
                    var planItem = caseModel.PlanItems.FirstOrDefault(pi => pi.Id == token.CurrentPlanItemId);
                    if (planItem?.Type == "eventListener" && planItem.DefinitionRef == "caseFileItemUpdate")
                    {
                        var newToken = token with
                        {
                            CaseFile = new Dictionary<string, object>(token.CaseFile) { [caseFileItemId] = newValue }
                        };
                        await ProcessCaseTokenAsync(newToken, caseModel, new List<string>(), cancellationToken);
                    }
                }
            }
            catch (Exception ex)
            {
                span.SetStatus(Status.Error.WithDescription(ex.Message));
                _logger.LogError(ex, "Failed to update CaseFileItem {CaseFileItemId} in case {CaseId}", caseFileItemId, caseId);
                throw new DistributedTokenException($"Failed to update CaseFileItem {caseFileItemId} in case {caseId}", ex);
            }
        }

        public async Task TriggerUserEventAsync(string caseId, string eventId, Dictionary<string, object> eventData, CancellationToken cancellationToken = default)
        {
            try
            {
                var cmmnXml = await _store.GetCmmnModelAsync(caseId)
                    ?? throw new DistributedTokenException($"CMMN model {caseId} not found");
                var caseModel = await _cmmnParser.ParseAsync(cmmnXml);

                var planItem = caseModel.PlanItems.FirstOrDefault(pi => pi.Id == eventId && pi.Type == "eventListener" && pi.DefinitionRef == "userEventListener")
                    ?? throw new DistributedTokenException($"UserEventListener {eventId} not found");

                var caseTokens = await _store.GetPendingCaseTokensAsync();
                var caseToken = caseTokens.FirstOrDefault(t => t.CaseInstanceId == Guid.Parse(caseId));
                if (caseToken.Id != default)
                {
                    var updatedCaseFile = new Dictionary<string, object>(caseToken.CaseFile);
                    foreach (var kvp in eventData)
                        updatedCaseFile[kvp.Key] = kvp.Value;

                    var newToken = new CaseToken(
                        Guid.NewGuid(),
                        caseToken.CaseInstanceId,
                        planItem.Id,
                        planItem.Type,
                        updatedCaseFile,
                        DateTime.UtcNow
                    );
                    await DistributeCaseTokenAsync(newToken, cancellationToken);
                    _logger.LogInformation("Triggered user event {EventId} for case {CaseId}", eventId, caseId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to trigger user event {EventId} for case {CaseId}", eventId, caseId);
                throw new DistributedTokenException($"Failed to trigger user event {eventId} for case {caseId}", ex);
            }
        }

        public async Task GenerateAdHocSubprocessAsync(string caseId, CancellationToken cancellationToken = default)
        {
            using var span = _tracer.StartActiveSpan("GenerateAdHocSubprocess");
            span.SetAttribute("caseId", caseId);
            try
            {
                var cmmnXml = await _store.GetCmmnModelAsync(caseId)
                              ?? throw new DistributedTokenException($"CMMN model {caseId} not found");
                var caseModel = await _cmmnParser.ParseAsync(cmmnXml);

                var caseTokens = await _store.GetPendingCaseTokensAsync();
               var caseToken = caseTokens.FirstOrDefault(t => t.CaseInstanceId == Guid.Parse(caseId));
               if (caseToken == null)
               {
                   throw new DistributedTokenException($"No active tokens found for case {caseId}");
                }

               // Prädiktive Optimierung mit externem Kontext
               var historicalData = await _store.GetHistoricalCaseDataAsync(caseId);
               var predictedPlanItems = await _aiDecisionService.PredictOptimalPlanItemsAsync(caseId, caseToken.CaseFile, historicalData, cancellationToken);

               foreach (var planItem in predictedPlanItems)
               {
                   await AddDiscretionaryItemAsync(caseId, planItem with { IsDiscretionary = true }, cancellationToken);
                   _logger.LogInformation("Added AI-predicted PlanItem {PlanItemId} to case {CaseId}", planItem.Id, caseId);
               }

               // Fallback auf Ad-hoc-Subprozess, falls keine prädiktiven Vorschläge
               if (!predictedPlanItems.Any())
               {
                   var adHocSubprocess = await _aiDecisionService.GenerateAdHocSubprocessAsync(caseId, caseToken.CaseFile, cancellationToken);
                   await AddDiscretionaryItemAsync(caseId, adHocSubprocess with { IsDiscretionary = true }, cancellationToken);
                   _logger.LogInformation("Added AI-generated ad-hoc subprocess {PlanItemId} to case {CaseId}", adHocSubprocess.Id, caseId);
               }
               // Speichere historische Daten
               var completedPlanItems = caseModel.PlanItems.Where(pi => pi.Type != "eventListener").Select(pi => pi.Id).ToList();
               var historicalDataEntry = new HistoricalCaseData(caseId, caseToken.CaseFile, completedPlanItems, DateTime.UtcNow);
               await _store.SaveHistoricalCaseDataAsync(historicalDataEntry);

               span.SetStatus(Status.Ok);

            }
            catch (Exception ex)
            {
                span.SetStatus(Status.Error.WithDescription(ex.Message));
                _logger.LogError(ex, "Failed to generate ad-hoc subprocess for case {CaseId}", caseId);
                throw new DistributedTokenException($"Failed to generate ad-hoc subprocess for case {caseId}", ex);
            }
        }


        private async Task<WorkerNode?> FindBestWorkerAsync(string nodeType)
        {
            var workers = await _store.GetActiveWorkersAsync() ?? new List<WorkerNode>();
            if (workers.Count == 0)
                return null; // gracefully handle absence instead of throwing via LINQ on null
            return workers
                .Where(w => nodeType switch
                {
                    "businessRuleTask" => w.SupportsDmn,
                    "humanTask" or "caseTask" => w.SupportsCmmn,
                    _ => w.SupportedNodeTypes.Contains(nodeType)
                })

                .Where(w => w.CurrentLoad < w.MaxCapacity)
                .Where(w => DateTime.UtcNow - w.LastHeartbeat < TimeSpan.FromMinutes(2))
                .OrderBy(w => w.CurrentLoad)
                .FirstOrDefault();
        }
        
        private async Task ProcessDistributedTokensAsync(BpmnModel model, List<string> trace, CancellationToken cancellationToken)
        {
            const int maxIterations = 50;
            var iteration = 0;

            while (iteration < maxIterations && !cancellationToken.IsCancellationRequested)
            {
                var pendingTokens = await GetPendingTokensAsync(cancellationToken);
                if (!pendingTokens.Any())
                    break;

                foreach (var token in pendingTokens)
                {
                    await ProcessTokenAsync(token, model, trace, cancellationToken);
                }

                iteration++;
                await Task.Delay(100, cancellationToken);
            }
        }


        private async Task ProcessDistributedCaseTokensAsync(CaseModel model, List<string> trace, CancellationToken cancellationToken)
        {
            const int maxIterations = 50;
            var iteration = 0;

            while (iteration < maxIterations && !cancellationToken.IsCancellationRequested)
            {
                var pendingTokens = await GetPendingCaseTokensAsync(cancellationToken);
                if (!pendingTokens.Any())
                    break;

                await Parallel.ForEachAsync(pendingTokens, cancellationToken, async (token, ct) =>
                {
                    await ProcessCaseTokenAsync(token, model, trace, ct);
                });

                iteration++;
                await Task.Delay(100, cancellationToken);
            }
        }

        private async Task ProcessTokenAsync(ExecutionToken token, BpmnModel model, List<string> trace, CancellationToken cancellationToken)
        {
            const int maxRetries = 3;
            var retryCount = 0;

            while (retryCount < maxRetries)
            {
                try
                {
                    _processingTokens[token.Id] = token;
                    var currentNode = FindNode(model, token.CurrentNodeId);
                    if (currentNode == null)
                    {
                        trace.Add($"NodeNotFound: {token.CurrentNodeId}");
                        return;
                    }

                    switch (currentNode)
                    {
                        case BpmnEvent evt:
                            await ProcessEventAsync(evt, token, model, trace, cancellationToken);
                            break;
                        case BpmnTask task:
                            await ProcessTaskAsync(task, token, model, trace, cancellationToken);
                            break;
                        case BpmnGateway gateway:
                            await ProcessGatewayAsync(gateway, token, model, trace, cancellationToken);
                            break;
                        case BpmnSubprocess subprocess:
                            await ProcessSubprocessAsync(subprocess, token, model, trace, cancellationToken);
                            break;
                    }

                    if (token.AssignedWorker != null)
                    {
                        var worker = await _store.GetWorkerAsync(token.AssignedWorker);
                        if (worker != null)
                        {
                            await _store.SaveWorkerAsync(worker with { CurrentLoad = Math.Max(0, worker.CurrentLoad - 1) });
                        }
                    }
                    return;
                }
                catch (Exception ex)
                {
                    retryCount++;
                    if (retryCount >= maxRetries)
                    {
                        _logger.LogError(ex, "Max retries reached for token {TokenId}", token.Id);
                        await _store.SaveToDeadLetterQueueAsync(token, ex.Message);
                        trace.Add($"TokenFailed: {token.Id} - {ex.Message}");
                        return;
                    }
                    await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, retryCount)), cancellationToken);
                    _logger.LogWarning(ex, "Retry {RetryCount}/{MaxRetries} for token {TokenId}", retryCount, maxRetries, token.Id);
                }
                finally
                {
                    _processingTokens.TryRemove(token.Id, out _);
                }
            }
        }


        private async Task ProcessCaseTokenAsync(CaseToken token, CaseModel model, List<string> trace, CancellationToken cancellationToken)
        {
            using var span = _tracer.StartActiveSpan("ProcessCaseToken");
            span.SetAttribute("tokenId", token.Id.ToString());
            span.SetAttribute("planItemId", token.CurrentPlanItemId);

            const int maxRetries = 3;
            var retryCount = 0;

            while (retryCount < maxRetries)
            {
                try
                {
                    _processingCaseTokens[token.Id] = token;
                    var planItem = model.PlanItems.FirstOrDefault(pi => pi.Id == token.CurrentPlanItemId)
                        ?? throw new DistributedTokenException($"PlanItem {token.CurrentPlanItemId} not found");

                    if (!await EvaluateSentriesAsync(planItem.EntrySentryRefs, model, token.CaseFile, cancellationToken))
                    {
                        trace.Add($"CaseTokenBlocked: {token.Id} - Entry sentries not satisfied for {planItem.Id}");
                        span.SetStatus(Status.Ok);
                        return;
                    }

                    switch (planItem.Type.ToLowerInvariant())
                    {
                        case "humantask":
                            await ProcessHumanTaskAsync(planItem, token, model, trace, cancellationToken);
                            break;
                        case "processtask":
                            await ProcessProcessTaskAsync(planItem, token, model, trace, cancellationToken);
                            break;
                        case "casetask":
                            await ProcessCaseTaskAsync(planItem, token, model, trace, cancellationToken);
                            break;
                        case "milestone":
                            trace.Add($"MilestoneReached: {planItem.Id}");
                            await CompletePlanItemAsync(planItem, token, model, trace, cancellationToken);
                            break;
                        case "eventlistener" when planItem.DefinitionRef == "timerEventListener":
                            var duration = planItem.Attributes?.GetValueOrDefault("timeDuration", "PT1M");
                            await Task.Delay(ParseDuration(duration), cancellationToken);
                            trace.Add($"TimerEventListenerTriggered: {planItem.Id} after {duration}");
                            await CompletePlanItemAsync(planItem, token, model, trace, cancellationToken);
                            break;
                        case "eventlistener" when planItem.DefinitionRef == "caseFileItemUpdate":
                            trace.Add($"CaseFileItemUpdateListener: {planItem.Id} triggered");
                            await CompletePlanItemAsync(planItem, token, model, trace, cancellationToken);
                            break;
                        case "eventlistener" when planItem.DefinitionRef == "userEventListener":
                            trace.Add($"UserEventListenerTriggered: {planItem.Id}");
                            await CompletePlanItemAsync(planItem, token, model, trace, cancellationToken);
                            break;
                        case "adhocsubprocess":
                            trace.Add($"AdHocSubprocess: {planItem.Id} started");
                            // Dynamische Logik basierend auf AI-generierten Attributen
                            var subTasks = planItem.Attributes?.GetValueOrDefault("subTasks", "").Split(';').Select(id => new PlanItem(
                                $"subtask_{id}", "humanTask", "humanTaskDef", new() { { "camunda:assignee", "user1" } }, null, null, true
                            )).ToList() ?? [];
                            // MCP-Aktion für externe Systeme
                            if (planItem.Attributes?.ContainsKey("mcpAction") == true)
                            {
                                var mcpServerUrl = planItem.Attributes.GetValueOrDefault("mcpServerUrl", "http://mcp-server:8080/api/mcp");
                                var mcpMethod = planItem.Attributes["mcpAction"];
                                var mcpParams = new Dictionary<string, object>
                                {
                                    { "caseId", model.Id },
                                    { "planItemId", planItem.Id }
                                };
                                await _aiDecisionService.ExecuteMcpActionAsync(model.Id, mcpServerUrl, mcpMethod, mcpParams, cancellationToken);
                                trace.Add($"MCPActionTriggered: {mcpMethod} on {mcpServerUrl}");
                            }

                            foreach (var subTask in subTasks)
                            {
                                await AddDiscretionaryItemAsync(model.Id, subTask, cancellationToken);
                                trace.Add($"AdHocSubprocessTaskAdded: {subTask.Id}");
                            }
                            await CompletePlanItemAsync(planItem, token, model, trace, cancellationToken);
                            break;
                        default:
                            trace.Add($"UnsupportedPlanItem: {planItem.Type} {planItem.Id}");
                            await CompletePlanItemAsync(planItem, token, model, trace, cancellationToken);
                            break;
                    }

                    if (await EvaluateSentriesAsync(planItem.ExitSentryRefs, model, token.CaseFile, cancellationToken))
                    {
                        trace.Add($"ExitSentryTriggered: {planItem.Id}");
                        await CompletePlanItemAsync(planItem, token, model, trace, cancellationToken);
                    }

                    if (token.AssignedWorker != null)
                    {
                        var worker = await _store.GetWorkerAsync(token.AssignedWorker);
                        if (worker != null)
                        {
                            await _store.SaveWorkerAsync(worker with { CurrentLoad = Math.Max(0, worker.CurrentLoad - 1) });
                        }
                    }
                    span.SetStatus(Status.Ok);
                    return;
                }
                catch (Exception ex)
                {
                    retryCount++;
                    if (retryCount >= maxRetries)
                    {
                        _logger.LogError(ex, "Max retries reached for case token {TokenId}", token.Id);
                        await _store.SaveToDeadLetterQueueAsync(token, ex.Message);
                        trace.Add($"CaseTokenFailed: {token.Id} - {ex.Message}");
                        span.SetStatus( Status.Error.WithDescription( ex.Message));
                        return;
                    }
                    await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, retryCount)), cancellationToken);
                    _logger.LogWarning(ex, "Retry {RetryCount}/{MaxRetries} for case token {TokenId}", retryCount, maxRetries, token.Id);
                }
                finally
                {
                    _processingCaseTokens.TryRemove(token.Id, out _);
                }
            }
        }

        private async Task ProcessEventAsync(BpmnEvent evt, ExecutionToken token, BpmnModel model, List<string> trace, CancellationToken cancellationToken)
        {
            switch (evt.Type)
            {
                case "startEvent":
                    trace.Add($"StartEvent: {evt.Id}");
                    await ContinueToNextNodeAsync(evt.Id, token, model, trace, cancellationToken);
                    break;

                case "endEvent":
                    trace.Add($"EndEvent: {evt.Id}");
                    break;

                case "intermediateCatchEvent" when evt.EventDefinitionType == "timer":
                    var duration = evt.Attributes?.TryGetValue("timeDuration", out var dur) == true ? dur.ToString() : "PT1M";
                    await Task.Delay(ParseDuration(duration), cancellationToken);
                    trace.Add($"TimerEvent: {evt.Id} triggered after {duration}");
                    await ContinueToNextNodeAsync(evt.Id, token, model, trace, cancellationToken);
                    break;

                case "boundaryEvent" when evt.EventDefinitionType == "timer":
                    if (evt.AttachedToRef != null && model.Tasks.Any(t => t.Id == evt.AttachedToRef))
                    {
                        trace.Add($"BoundaryTimerEvent: {evt.Id} attached to {evt.AttachedToRef}");
                        if (evt.CancelActivity)
                        {
                            await ContinueToNextNodeAsync(evt.Id, token, model, trace, cancellationToken);
                        }
                    }
                    break;

                case "intermediateCatchEvent" when evt.EventDefinitionType == "message":
                    var messageName = evt.Attributes?.TryGetValue("messageRef", out var msg) == true ? msg.ToString() : null;
                    if (messageName != null)
                    {
                        trace.Add($"MessageEvent: {evt.Id} waiting for message {messageName}");
                        await _messageDispatcher.SubscribeToMessageAsync(messageName, async (msg) =>
                        {
                            var newToken = token with { Variables = new Dictionary<string, object>(msg.Variables) };
                            await ContinueToNextNodeAsync(evt.Id, newToken, model, trace, cancellationToken);
                        }, cancellationToken);
                    }
                    break;

                default:
                    trace.Add($"UnsupportedEvent: {evt.Type} {evt.Id}");
                    await ContinueToNextNodeAsync(evt.Id, token, model, trace, cancellationToken);
                    break;
            }
        }

        private async Task ProcessTaskAsync(BpmnTask task, ExecutionToken token, BpmnModel model, List<string> trace, CancellationToken cancellationToken)
        {
            trace.Add($"DistributedTask: {task.Type} {task.Id} on worker {token.AssignedWorker}");
            bool isAsync = task.Attributes?.ContainsKey("camunda:async") == true && task.Attributes["camunda:async"] == "true" ||
                           task.Attributes?.ContainsKey("flowable:async") == true && task.Attributes["flowable:async"] == "true";

            if (isAsync)
            {
                trace.Add($"AsyncTask: {task.Id} queued for async processing");
                await _messageDispatcher.QueueTaskAsync(task.Id, task.Type, token.Variables, cancellationToken);
                return;
            }

            switch (task.Type.ToLowerInvariant())
            {
                case "scripttask":
                    if (model.ProcessVariables != null)
                        foreach (var kv in model.ProcessVariables)
                            token.Variables[kv.Key] = kv.Value;
                    await ScriptTaskExecution.TryHandleScriptTaskAsync(task, token.Variables, cancellationToken);
                    if (model.ProcessVariables != null)
                        foreach (var kv in token.Variables)
                            model.ProcessVariables[kv.Key] = kv.Value;
                    trace.Add($"ScriptTaskCompleted: {task.Id}");
                    await ContinueToNextNodeAsync(task.Id, token, model, trace, cancellationToken);
                    break;

                case "servicetask":
                    var attributes = task.Attributes ?? new Dictionary<string, string>();
                    var variables = token.Variables ?? new Dictionary<string, object>();

                    if (attributes.TryGetValue("zeebe:taskDefinition", out var taskDef))
                        attributes["implementation"] = taskDef;

                    if (_serviceRegistry.TryResolve(attributes.GetValueOrDefault("implementation", ""), out var handler))
                    {
                        trace.Add($"ServiceTask: local handler found for {task.Implementation}");
                        await handler.ExecuteAsync(attributes, variables, cancellationToken);
                        trace.Add($"ServiceTaskCompleted(local): {task.Id}");
                    }
                    else
                    {
                        var targetWorker = token.AssignedWorker ?? (await FindBestWorkerAsync(task.Type))?.Id;
                        await _messageDispatcher.DispatchServiceTaskAsync(targetWorker ?? "", attributes.GetValueOrDefault("implementation", ""), attributes, variables, cancellationToken);
                        trace.Add($"ServiceTaskDispatched: {task.Id} -> {targetWorker ?? "none"}");
                    }

                    if (model.ProcessVariables == null)
                        model.ProcessVariables = new Dictionary<string, object>(variables);
                    else
                        foreach (var kv in variables)
                            model.ProcessVariables[kv.Key] = kv.Value;
                    token = token with { Variables = new Dictionary<string, object>(variables) };
                    await ContinueToNextNodeAsync(task.Id, token, model, trace, cancellationToken);
                    break;

                case "usertask":
                    attributes = task.Attributes ?? new Dictionary<string, string>();
                    variables = token.Variables ?? new Dictionary<string, object>();
                    string? assignee = null;
                    if (attributes?.TryGetValue("camunda:assignee", out assignee) == true ||
                        attributes?.TryGetValue("flowable:assignee", out assignee) == true ||
                        attributes?.TryGetValue("cib:assignee", out assignee) == true)
                    {
                        trace.Add($"UserTask: {task.Id} assigned to {assignee}");
                        await _messageDispatcher.DispatchUserTaskAsync(assignee, task.Id, variables, cancellationToken);
                    }
                    else
                    {
                        trace.Add($"UserTask: {task.Id} no assignee defined");
                    }
                    break;

                case "businessruletask":
                    attributes = task.Attributes ?? new Dictionary<string, string>();
                    string? decisionRef = null;
                    string? resultVariable = null;
                    if (attributes?.TryGetValue("camunda:decisionRef", out decisionRef) == true ||
                        attributes?.TryGetValue("flowable:decisionRef", out decisionRef) == true)
                    {
                        attributes.TryGetValue("camunda:resultVariable", out resultVariable);
                        resultVariable ??= attributes.TryGetValue("flowable:resultVariable", out var flowableResult) ? flowableResult : "decisionResult";

                        trace.Add($"BusinessRuleTask: {task.Id} evaluating decision {decisionRef}");
                        try
                        {
                            var targetWorker = token.AssignedWorker ?? (await FindBestWorkerAsync(task.Type))?.Id;
                            if (targetWorker != null && (await _store.GetWorkerAsync(targetWorker))?.SupportsDmn == true)
                            {
                                trace.Add($"BusinessRuleTask: dispatching to DMN-capable worker {targetWorker}");
                                var decisionResult = await _messageDispatcher.DispatchDmnTaskAsync(targetWorker, decisionRef, token.Variables, cancellationToken);
                                token.Variables[resultVariable] = decisionResult;
                                if (model.ProcessVariables == null)
                                    model.ProcessVariables = new Dictionary<string, object>();
                                model.ProcessVariables[resultVariable] = decisionResult;
                                trace.Add($"BusinessRuleTaskCompleted: {task.Id} result stored in {resultVariable}");
                            }
                            else
                            {
                                var dmnXml = await _store.GetDmnModelAsync(decisionRef, cancellationToken)
                                    ?? throw new DistributedTokenException($"DMN model {decisionRef} not found");
                                var decision = await _dmnParser.ParseAsync(dmnXml, cancellationToken);
                                var decisionResult = await _dmnEngine.EvaluateDecisionAsync(decision, token.Variables);
                                token.Variables[resultVariable] = decisionResult;
                                if (model.ProcessVariables == null)
                                    model.ProcessVariables = new Dictionary<string, object>();
                                model.ProcessVariables[resultVariable] = decisionResult;
                                trace.Add($"BusinessRuleTaskCompleted: {task.Id} result stored in {resultVariable}");
                            }
                        }
                        catch (DmnParseException ex)
                        {
                            _logger.LogError(ex, "Failed to parse DMN model {DecisionRef} for task {TaskId}", decisionRef, task.Id);
                            throw new DistributedTokenException($"Failed to parse DMN model {decisionRef} for task {task.Id}", ex);
                        }
                        catch (DmnEvaluationException ex)
                        {
                            _logger.LogError(ex, "Failed to evaluate DMN model {DecisionRef} for task {TaskId}", decisionRef, task.Id);
                            throw new DistributedTokenException($"Failed to evaluate DMN model {decisionRef} for task {task.Id}", ex);
                        }
                    }
                    else
                    {
                        trace.Add($"BusinessRuleTask: {task.Id} no decisionRef defined");
                    }
                    await ContinueToNextNodeAsync(task.Id, token, model, trace, cancellationToken);
                    break;

                default:
                    await ContinueToNextNodeAsync(task.Id, token, model, trace, cancellationToken);
                    break;
            }
        }

        private async Task ProcessHumanTaskAsync(PlanItem planItem, CaseToken token, CaseModel model, List<string> trace, CancellationToken cancellationToken)
        {
            var attributes = planItem.Attributes ?? new Dictionary<string, string>();
            string? assignee = null;
            if (attributes.TryGetValue("camunda:assignee", out assignee) ||
                attributes.TryGetValue("flowable:assignee", out assignee))
            {
                trace.Add($"HumanTask: {planItem.Id} assigned to {assignee}");
                await _messageDispatcher.DispatchUserTaskAsync(assignee, planItem.Id, token.CaseFile, cancellationToken);
            }
            else
            {
                trace.Add($"HumanTask: {planItem.Id} no assignee defined");
            }
            await CompletePlanItemAsync(planItem, token, model, trace, cancellationToken);
        }

        private async Task ProcessProcessTaskAsync(PlanItem planItem, CaseToken token, CaseModel model, List<string> trace, CancellationToken cancellationToken)
        {
            var attributes = planItem.Attributes ?? new Dictionary<string, string>();
            string processRef;
            if (attributes.TryGetValue("camunda:processRef", out processRef) ||
                attributes.TryGetValue("flowable:processRef", out processRef))
            {
                trace.Add($"ProcessTask: {planItem.Id} starting process {processRef}");
                var bpmnModel = new BpmnModel(processRef, processRef,[], [], [], [], []); // Placeholder, lade echtes Modell
                var processTrace = await ExecuteAsync(bpmnModel, cancellationToken);
                trace.AddRange(processTrace);
            }
            await CompletePlanItemAsync(planItem, token, model, trace, cancellationToken);
        }

        private async Task ProcessCaseTaskAsync(PlanItem planItem, CaseToken token, CaseModel model, List<string> trace, CancellationToken cancellationToken)
        {
            var attributes = planItem.Attributes ?? new Dictionary<string, string>();
            string caseRef;
            if (attributes.TryGetValue("camunda:caseRef", out caseRef) ||
                attributes.TryGetValue("flowable:caseRef", out caseRef))
            {
                trace.Add($"CaseTask: {planItem.Id} starting case {caseRef}");
                var cmmnXml = await _store.GetCmmnModelAsync(caseRef)
                    ?? throw new DistributedTokenException($"CMMN model {caseRef} not found");
                var caseModel = await _cmmnParser.ParseAsync(cmmnXml);
                var caseTrace = await ExecuteCaseAsync(caseModel, cancellationToken);
                trace.AddRange(caseTrace);
            }
            await CompletePlanItemAsync(planItem, token, model, trace, cancellationToken);
        }

        private async Task CompletePlanItemAsync(PlanItem planItem, CaseToken token, CaseModel model, List<string> trace, CancellationToken cancellationToken)
        {
            trace.Add($"PlanItemCompleted: {planItem.Id}");
            foreach (var dependentItem in model.PlanItems.Where(pi => pi.EntrySentryRefs?.Any(sr => model.Sentries.Any(s => s.Id == sr && s.OnPartRef == planItem.Id)) == true))
            {
                var newToken = new CaseToken(
                    Guid.NewGuid(),
                    token.CaseInstanceId,
                    dependentItem.Id,
                    dependentItem.Type,
                    new Dictionary<string, object>(token.CaseFile),
                    DateTime.UtcNow
                );
                await DistributeCaseTokenAsync(newToken, cancellationToken);
                trace.Add($"CaseTokenDistributed: {newToken.Id} -> {dependentItem.Id}");
            }
        }

        private async Task ProcessSubprocessAsync(BpmnSubprocess subprocess, ExecutionToken token, BpmnModel model, List<string> trace, CancellationToken cancellationToken)
        {
            trace.Add($"Subprocess: {subprocess.Id}");
            if (subprocess.IsMultiInstance)
            {
                int cardinality = subprocess.LoopCardinality ?? 1;
                for (int i = 0; i < cardinality; i++)
                {
                    var instanceToken = new ExecutionToken(
                        Guid.NewGuid(),
                        token.ProcessInstanceId,
                        subprocess.Id,
                        "subprocess",
                        new Dictionary<string, object>(token.Variables),
                        DateTime.UtcNow
                    );
                    await DistributeTokenAsync(instanceToken, cancellationToken);
                    trace.Add($"MultiInstanceSubprocess: {subprocess.Id} instance {i + 1}");
                }
            }
            else
            {
                await ContinueToNextNodeAsync(subprocess.Id, token, model, trace, cancellationToken);
            }
        }

        private async Task ProcessGatewayAsync(BpmnGateway gateway, ExecutionToken token, BpmnModel model, List<string> trace, CancellationToken cancellationToken)
        {
            var outgoingFlows = model.SequenceFlows.Where(f => f.SourceRef == gateway.Id).ToList();
            switch (gateway.Type)
            {
                case "parallelGateway":
                    foreach (var flow in outgoingFlows)
                    {
                        var newToken = new ExecutionToken(
                            Guid.NewGuid(),
                            token.ProcessInstanceId,
                            flow.TargetRef,
                            GetNodeType(model, flow.TargetRef),
                            new Dictionary<string, object>(token.Variables),
                            DateTime.UtcNow
                        );
                        await DistributeTokenAsync(newToken, cancellationToken);
                        trace.Add($"ParallelBranch: {flow.TargetRef}");
                    }
                    break;

                case "exclusiveGateway":
                    foreach (var flow in outgoingFlows)
                    {
                        var condition = flow.Attributes?.TryGetValue("conditionExpression", out var expr) == true ? expr.ToString() : null;
                        if (condition == null || await EvaluateConditionAsync(condition, token.Variables))
                        {
                            var newToken = token with
                            {
                                CurrentNodeId = flow.TargetRef,
                                NodeType = GetNodeType(model, flow.TargetRef)
                            };
                            await DistributeTokenAsync(newToken, cancellationToken);
                            trace.Add($"ExclusiveBranch: {flow.TargetRef}");
                            break;
                        }
                    }
                    break;

                case "inclusiveGateway":
                    bool atLeastOneBranch = false;
                    foreach (var flow in outgoingFlows)
                    {
                        var condition = flow.Attributes?.TryGetValue("conditionExpression", out var expr) == true ? expr.ToString() : null;
                        if (condition == null || await EvaluateConditionAsync(condition, token.Variables))
                        {
                            var newToken = new ExecutionToken(
                                Guid.NewGuid(),
                                token.ProcessInstanceId,
                                flow.TargetRef,
                                GetNodeType(model, flow.TargetRef),
                                new Dictionary<string, object>(token.Variables),
                                DateTime.UtcNow
                            );
                            await DistributeTokenAsync(newToken, cancellationToken);
                            trace.Add($"InclusiveBranch: {flow.TargetRef}");
                            atLeastOneBranch = true;
                        }
                    }
                    if (!atLeastOneBranch)
                        throw new DistributedTokenException($"No valid branch for inclusiveGateway {gateway.Id}");
                    break;

                default:
                    throw new DistributedTokenException($"Unsupported gateway type: {gateway.Type}");
            }
        }

        private async Task ContinueToNextNodeAsync(string currentNodeId, ExecutionToken token, BpmnModel model, List<string> trace, CancellationToken cancellationToken)
        {
            var outgoingFlows = model.SequenceFlows.Where(f => f.SourceRef == currentNodeId).ToList();
            foreach (var flow in outgoingFlows)
            {
                trace.Add($"SequenceFlow: {flow.Id}");
                var newToken = token with
                {
                    CurrentNodeId = flow.TargetRef,
                    NodeType = GetNodeType(model, flow.TargetRef)
                };
                await DistributeTokenAsync(newToken, cancellationToken);
            }
        }

        private async Task<bool> EvaluateConditionAsync(string condition, Dictionary<string, object> variables)
        {
            try
            {
                var engine = new Jint.Engine();
                foreach (var kvp in variables)
                    engine.SetValue(kvp.Key, kvp.Value);
                return engine.Evaluate(condition).AsBoolean();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to evaluate condition: {Condition}", condition);
                return false;
            }
        }

        private async Task<bool> EvaluateSentriesAsync(List<string>? sentryRefs, CaseModel model, Dictionary<string, object> caseFile, CancellationToken cancellationToken)
        {
            if (sentryRefs == null || sentryRefs.Count == 0)
                return true;

            foreach (var sentryId in sentryRefs)
            {
                var sentry = model.Sentries.FirstOrDefault(s => s.Id == sentryId);
                if (sentry == null)
                    continue;

                bool allConditionsMet = sentry.Conditions.All(c => c.LogicalOperator == "OR");
                foreach (var condition in sentry.Conditions)
                {
                    bool conditionMet = false;
                    if (!string.IsNullOrEmpty(condition.VariableRef) && caseFile.TryGetValue(condition.VariableRef, out var value))
                    {
                        var engine = _jintCache.GetOrAdd(condition.Expression, _ => new Jint.Engine());
                        engine.SetValue("input", value);
                        conditionMet = engine.Evaluate(condition.Expression).AsBoolean();
                    }

                    if (!string.IsNullOrEmpty(sentry.OnPartRef) && !string.IsNullOrEmpty(condition.OnPartEvent))
                    {
                        var onPartItem = model.PlanItems.FirstOrDefault(pi => pi.Id == sentry.OnPartRef);
                        if (onPartItem != null)
                        {
                            // Prüfe Zustand des OnPart-Items (z.B. complete, occur)
                            var tokens = await _store.GetPendingCaseTokensAsync();
                            conditionMet &= tokens.Any(t => t.CurrentPlanItemId == sentry.OnPartRef && condition.OnPartEvent == "complete");
                        }
                    }

                    if (condition.LogicalOperator == "AND" && !conditionMet)
                        return false;
                    if (condition.LogicalOperator == "OR" && conditionMet)
                        allConditionsMet = true;
                }

                if (sentry.Conditions.Any(c => c.LogicalOperator == "OR") && !allConditionsMet)
                    return false;
            }
            return true;
        }


        private object? FindNode(BpmnModel model, string nodeId)
        {
            return model.Events.FirstOrDefault(e => e.Id == nodeId) as object
                ?? model.Tasks.FirstOrDefault(t => t.Id == nodeId) as object
                ?? model.Gateways.FirstOrDefault(g => g.Id == nodeId) as object
                ?? model.Subprocesses.FirstOrDefault(s => s.Id == nodeId) as object;
        }

        private string GetNodeType(BpmnModel model, string nodeId)
        {
            if (model.Events.Any(e => e.Id == nodeId))
                return model.Events.First(e => e.Id == nodeId).Type;
            if (model.Tasks.Any(t => t.Id == nodeId))
                return model.Tasks.First(t => t.Id == nodeId).Type;
            if (model.Gateways.Any(g => g.Id == nodeId))
                return model.Gateways.First(g => g.Id == nodeId).Type;
            if (model.Subprocesses.Any(s => s.Id == nodeId))
                return "subprocess";
            return "unknown";
        }

        private async void ProcessHeartbeatsAsync(object? state)
        {
            try
            {
                var cutoffTime = DateTime.UtcNow.AddMinutes(-2);
                var workers = await _store.GetActiveWorkersAsync();
                var deadWorkers = workers.Where(w => w.LastHeartbeat < cutoffTime).Select(w => w.Id).ToList();

                foreach (var deadWorkerId in deadWorkers)
                {
                    await UnregisterWorkerAsync(deadWorkerId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process heartbeats");
            }
        }

        private TimeSpan ParseDuration(string duration)
        {
            // Vereinfachte ISO 8601 Parser (für Produktion: System.Xml.XmlConvert verwenden)
            if (duration.StartsWith("PT"))
            {
                var timePart = duration.Substring(2);
                if (timePart.EndsWith("S"))
                    return TimeSpan.FromSeconds(double.Parse(timePart.TrimEnd('S')));
                if (timePart.EndsWith("M"))
                    return TimeSpan.FromMinutes(double.Parse(timePart.TrimEnd('M')));
            }
            return TimeSpan.FromMinutes(1);
        }

        public void Dispose()
        {
            _heartbeatTimer?.Dispose();
        }


        //public async Task RegisterBpmnModelAsync(string processId, string bpmnXml, CancellationToken cancellationToken = default)
        //{
        //    await _store.SaveBpmnModelAsync(processId, bpmnXml);
        //    _logger.LogInformation("Registered BPMN model {ProcessId}", processId);
        //}

        //public async Task<List<string>> ExecuteProcessAsync(string processId, CancellationToken cancellationToken = default)
        //{
        //    var trace = new List<string>();
        //    var bpmnXml = await _store.GetBpmnModelAsync(processId);
        //    var processModel = await _bpmnParser.ParseAsync(bpmnXml, cancellationToken);
        //    var token = new CaseToken(
        //        Guid.NewGuid(),
        //        Guid.Parse(processModel.Id),
        //        processModel.PlanItems.FirstOrDefault(pi => pi.Type == "eventListener" && pi.DefinitionRef == "startEvent")?.Id
        //            ?? throw new DistributedTokenException("No start event found in process"),
        //        "eventListener",
        //        new Dictionary<string, object>(processModel.CaseFileItems.ToDictionary(cfi => cfi.Id, cfi => cfi.Value)),
        //        DateTime.UtcNow
        //    );

        //    await _store.SaveCaseTokenAsync(token);
        //    await ProcessCaseTokenAsync(token, processModel, trace, cancellationToken);
        //    return trace;
        //}

        //private async Task ProcessCaseTokenAsync(CaseToken token, Bpmn.BpmnModel model, List<string> trace, CancellationToken cancellationToken)
        //{
        //    using var span = _tracer.StartActiveSpan("ProcessCaseToken");
        //    span.SetAttribute("tokenId", token.Id.ToString());
        //    span.SetAttribute("planItemId", token.CurrentPlanItemId);

        //    const int maxRetries = 3;
        //    var retryCount = 0;

        //    while (retryCount < maxRetries)
        //    {
        //        try
        //        {
        //            _processingCaseTokens[token.Id] = token;
        //            var planItem = model.PlanItems.FirstOrDefault(pi => pi.Id == token.CurrentPlanItemId)
        //                ?? throw new DistributedTokenException($"PlanItem {token.CurrentPlanItemId} not found");

        //            if (!await EvaluateSentryAsync(planItem.EntrySentryRefs, model, token.CaseFile, cancellationToken))
        //            {
        //                trace.Add($"CaseTokenBlocked: {token.Id} - Entry sentries not satisfied for {planItem.Id}");
        //                span.SetStatus(Status.Ok);
        //                return;
        //            }

        //            switch (planItem.Type.ToLowerInvariant())
        //            {
        //                case "servicetask" or "mcpservicetask" when planItem.Attributes?.ContainsKey("type") == true:
        //                    var serviceTaskType = planItem.Attributes["type"];
        //                    var handler = _serviceRegistry.GetHandler(serviceTaskType);
        //                    await handler.ExecuteAsync(planItem.Attributes ?? new Dictionary<string, string>(), token.CaseFile, cancellationToken);
        //                    trace.Add($"ServiceTaskExecuted: {planItem.Id} (type: {serviceTaskType})");
        //                    await CompletePlanItemAsync(planItem, token, model, trace, cancellationToken);
        //                    break;
        //                case "usertask":
        //                    trace.Add($"UserTaskAssigned: {planItem.Id} to {planItem.Attributes?.GetValueOrDefault("camunda:assignee", "unassigned")}");
        //                    await CompletePlanItemAsync(planItem, token, model, trace, cancellationToken);
        //                    break;
        //                case "eventlistener" when planItem.DefinitionRef == "timerEventDefinition":
        //                    var duration = planItem.Attributes?.GetValueOrDefault("timeDuration", "PT1M");
        //                    await Task.Delay(ParseDuration(duration), cancellationToken);
        //                    trace.Add($"TimerEventListenerTriggered: {planItem.Id} after {duration}");
        //                    await CompletePlanItemAsync(planItem, token, model, trace, cancellationToken);
        //                    break;
        //                case "eventlistener" when planItem.DefinitionRef == "userEventListener" or "startEvent":
        //                    trace.Add($"UserEventListenerTriggered: {planItem.Id}");
        //                    await CompletePlanItemAsync(planItem, token, model, trace, cancellationToken);
        //                    break;
        //                case "adHocSubProcess":
        //                    trace.Add($"AdHocSubprocess: {planItem.Id} started");
        //                    var subTasks = planItem.Attributes?.GetValueOrDefault("subTasks", "").Split(';').Select(id => new PlanItem(
        //                        $"subtask_{id}", "humanTask", "humanTaskDef", new() { { "camunda:assignee", "user1" } }, null, null, true
        //                    )).ToList() ?? [];

        //                    if (planItem.Attributes?.ContainsKey("mcpAction") == true)
        //                    {
        //                        var mcpServerUrl = planItem.Attributes.GetValueOrDefault("mcpServerUrl", "http://mcp-server:8080/api/mcp");
        //                        var mcpMethod = planItem.Attributes["mcpAction"];
        //                        var mcpParams = new Dictionary<string, object>
        //                        {
        //                            { "caseId", model.Id },
        //                            { "planItemId", planItem.Id }
        //                        };
        //                        await _aiDecisionService.ExecuteMcpActionAsync(model.Id, mcpServerUrl, mcpMethod, mcpParams, cancellationToken);
        //                        trace.Add($"MCPActionTriggered: {mcpMethod} on {mcpServerUrl}");
        //                    }

        //                    foreach (var subTask in subTasks)
        //                    {
        //                        await AddDiscretionaryItemAsync(model.Id, subTask, cancellationToken);
        //                        trace.Add($"AdHocSubprocessTaskAdded: {subTask.Id}");
        //                    }
        //                    await CompletePlanItemAsync(planItem, token, model, trace, cancellationToken);
        //                    break;
        //                case "exclusiveGateway" or "parallelGateway":
        //                    trace.Add($"GatewayProcessed: {planItem.Id} ({planItem.Type})");
        //                    await CompletePlanItemAsync(planItem, token, model, trace, cancellationToken);
        //                    break;
        //                default:
        //                    trace.Add($"UnsupportedPlanItem: {planItem.Type} {planItem.Id}");
        //                    await CompletePlanItemAsync(planItem, token, model, trace, cancellationToken);
        //                    break;
        //            }

        //            if (await EvaluateSentryAsync(planItem.ExitSentryRefs, model, token.CaseFile, cancellationToken))
        //            {
        //                trace.Add($"ExitSentryTriggered: {planItem.Id}");
        //                await CompletePlanItemAsync(planItem, token, model, trace, cancellationToken);
        //            }

        //            if (token.AssignedWorker != null)
        //            {
        //                var worker = await _store.GetWorkerAsync(token.AssignedWorker);
        //                if (worker != null)
        //                {
        //                    await _store.SaveWorkerAsync(worker with { CurrentLoad = Math.Max(0, worker.CurrentLoad - 1) });
        //                }
        //            }
        //            span.SetStatus(Status.Ok);
        //            return;
        //        }
        //        catch (Exception ex)
        //        {
        //            retryCount++;
        //            if (retryCount >= maxRetries)
        //            {
        //                _logger.LogError(ex, "Max retries reached for case token {TokenId}", token.Id);
        //                await _store.SaveToDeadLetterQueueAsync(token, ex.Message);
        //                trace.Add($"CaseTokenFailed: {token.Id} - {ex.Message}");
        //                span.SetStatus(Status.Error, ex.Message);
        //                return;
        //            }
        //            await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, retryCount)), cancellationToken);
        //            _logger.LogWarning(ex, "Retry {RetryCount}/{MaxRetries} for case token {TokenId}", retryCount, maxRetries, token.Id);
        //        }
        //        finally
        //        {
        //            _processingCaseTokens.TryRemove(token.Id, out _);
        //        }
        //    }
        //}

        //private async Task<bool> EvaluateSentryAsync(IReadOnlyList<string> sentryRefs, Bpmn.BpmnModel model, IDictionary<string, object> caseFile, CancellationToken cancellationToken)
        //{
        //    if (sentryRefs == null || !sentryRefs.Any())
        //        return true;

        //    foreach (var sentryRef in sentryRefs)
        //    {
        //        var sentry = model.Sentries.FirstOrDefault(s => s.Id == sentryRef);
        //        if (sentry == null)
        //            continue;

        //        foreach (var condition in sentry.Conditions)
        //        {
        //            bool conditionMet = condition.Expression switch
        //            {
        //                var expr when expr.StartsWith("${") => EvaluateExpression(expr, caseFile), // Beispiel: Jint für EL
        //                "complete" => true,
        //                _ => false
        //            };
        //            if (!conditionMet)
        //                return false;
        //        }
        //    }
        //    return true;
        //}

        //private bool EvaluateExpression(string expression, IDictionary<string, object> caseFile)
        //{
        //    // Beispiel: Einfache Expression Language Evaluation (erweitern mit Jint oder ähnlichem)
        //    return true; // Platzhalter
        //}

        //private async Task CompletePlanItemAsync(PlanItem planItem, CaseToken token, Bpmn.BpmnModel model, List<string> trace, CancellationToken cancellationToken)
        //{
        //    var nextSentries = model.Sentries.Where(s => planItem.ExitSentryRefs?.Contains(s.Id) == true).ToList();
        //    foreach (var sentry in nextSentries)
        //    {
        //        var nextPlanItems = model.PlanItems.Where(pi => pi.EntrySentryRefs?.Contains(sentry.Id) == true).ToList();
        //        foreach (var nextPlanItem in nextPlanItems)
        //        {
        //            var newToken = token with
        //            {
        //                Id = Guid.NewGuid(),
        //                CurrentPlanItemId = nextPlanItem.Id,
        //                CurrentPlanItemType = nextPlanItem.Type
        //            };
        //            await _store.SaveCaseTokenAsync(newToken);
        //            await _messageDispatcher.PublishCaseTokenAsync(newToken, cancellationToken);
        //            trace.Add($"CaseTokenCreated: {newToken.Id} for {nextPlanItem.Id}");
        //        }
        //    }
        //}

        //private async Task AddDiscretionaryItemAsync(string caseId, PlanItem planItem, CancellationToken cancellationToken)
        //{
        //    // Platzhalter für adHocSubProcess
        //    await Task.CompletedTask;
        //}


        //private async void ProcessHeartbeatsAsync(object? state)
        //{
        //    try
        //    {
        //        var cutoffTime = DateTime.UtcNow.AddMinutes(-2);
        //        var workers = await _store.GetActiveWorkersAsync();
        //        var deadWorkers = workers.Where(w => w.LastHeartbeat < cutoffTime).Select(w => w.Id).ToList();

        //        foreach (var deadWorkerId in deadWorkers)
        //        {
        //            await UnregisterWorkerAsync(deadWorkerId);
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Failed to process heartbeats");
        //    }
        //}

        //private TimeSpan ParseDuration(string duration)
        //{
        //    // Vereinfachte ISO 8601 Parser (für Produktion: System.Xml.XmlConvert verwenden)
        //    if (duration.StartsWith("PT"))
        //    {
        //        var timePart = duration.Substring(2);
        //        if (timePart.EndsWith("S"))
        //            return TimeSpan.FromSeconds(double.Parse(timePart.TrimEnd('S')));
        //        if (timePart.EndsWith("M"))
        //            return TimeSpan.FromMinutes(double.Parse(timePart.TrimEnd('M')));
        //    }
        //    return TimeSpan.FromMinutes(1);
        //}

        //public void Dispose()
        //{
        //    _heartbeatTimer?.Dispose();
        //}

    }
}


//using Microsoft.Extensions.Logging;
//using System.Collections.Concurrent;
//using VertexBPMN.Core.Bpmn;
//using VertexBPMN.Core.Domain;
//using VertexBPMN.Core.Messaging;
//using VertexBPMN.Core.Scripting;
//using VertexBPMN.Core.Services;
//using Task = System.Threading.Tasks.Task;

//namespace VertexBPMN.Core.Engine;

///// <summary>
///// In-memory implementation of distributed token engine
///// In production, this would use Redis, RabbitMQ, or Apache Kafka
///// </summary>
//public class DistributedTokenEngine : IDistributedTokenEngine
//{
//    private readonly ConcurrentQueue<ExecutionToken> _tokenQueue = new();
//    private readonly ConcurrentDictionary<string, WorkerNode> _workers = new();
//    private readonly ConcurrentDictionary<Guid, ExecutionToken> _processingTokens = new();
//    private readonly ILogger<DistributedTokenEngine> _logger;
//    private readonly Timer _heartbeatTimer;
//    private readonly ServiceTaskRegistry _serviceRegistry;
//    private readonly IMessageDispatcher _messageDispatcher;

//    public DistributedTokenEngine(ILogger<DistributedTokenEngine> logger, ServiceTaskRegistry serviceRegistry, IMessageDispatcher dispatcher)
//    {
//        _logger = logger;
//        _serviceRegistry = serviceRegistry ?? throw new ArgumentNullException(nameof(serviceRegistry));
//        _messageDispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
//        // Initialize with current node as worker
//        var currentWorker = new WorkerNode(
//            Environment.MachineName,
//            Environment.MachineName,
//            5000,
//            DateTime.UtcNow,
//            new List<string> { "userTask", "serviceTask", "scriptTask", "businessRuleTask" },
//            0,
//            10
//        );
//        _workers[currentWorker.Id] = currentWorker;

//        // Start heartbeat timer
//        _heartbeatTimer = new Timer(ProcessHeartbeats, null, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));
//    }

//    public async Task<List<string>> ExecuteAsync(BpmnModel model, CancellationToken cancellationToken = default)
//    {
//        var trace = new List<string>();
//        var processInstanceId = Guid.NewGuid();

//        // Find start event
//        var startEvent = model.Events.FirstOrDefault(e => e.Type == "startEvent");
//        if (startEvent == null)
//        {
//            trace.Add("No start event found");
//            return trace;
//        }

//        trace.Add($"DistributedExecution: Starting process {processInstanceId}");

//        // Create initial token
//        var initialToken = new ExecutionToken(
//            Guid.NewGuid(),
//            processInstanceId,
//            startEvent.Id,
//            "startEvent",
//            new Dictionary<string, object>(),
//            DateTime.UtcNow
//        );

//        await DistributeTokenAsync(initialToken, cancellationToken);
//        trace.Add($"TokenDistributed: {initialToken.Id} -> {initialToken.CurrentNodeId}");

//        // Simulate distributed processing
//        await ProcessDistributedTokensAsync(model, trace, cancellationToken);

//        return trace;
//    }

//    public async Task<bool> CanExecuteAsync(string nodeId, CancellationToken cancellationToken = default)
//    {
//        // Check if any worker can handle this node type
//        var availableWorker = _workers.Values
//            .Where(w => w.CurrentLoad < w.MaxCapacity)
//            .Where(w => DateTime.UtcNow - w.LastHeartbeat < TimeSpan.FromMinutes(2))
//            .Any();

//        return await Task.FromResult(availableWorker);
//    }

//    public async Task DistributeTokenAsync(ExecutionToken token, CancellationToken cancellationToken = default)
//    {
//        // Find best worker for this token
//        var bestWorker = FindBestWorker(token.NodeType);

//        if (bestWorker != null)
//        {
//            var assignedToken = token with 
//            { 
//                AssignedWorker = bestWorker.Id, 
//                AssignedAt = DateTime.UtcNow 
//            };

//            _tokenQueue.Enqueue(assignedToken);
//            _logger.LogInformation("Token {TokenId} assigned to worker {WorkerId}", 
//                token.Id, bestWorker.Id);
//        }
//        else
//        {
//            // No worker available, queue for later
//            _tokenQueue.Enqueue(token);
//            _logger.LogWarning("No worker available for token {TokenId}, queued for later", token.Id);
//        }

//        await Task.CompletedTask;
//    }

//    public async Task<List<ExecutionToken>> GetPendingTokensAsync(CancellationToken cancellationToken = default)
//    {
//        var pendingTokens = new List<ExecutionToken>();

//        while (_tokenQueue.TryDequeue(out var token))
//        {
//            pendingTokens.Add(token);
//        }

//        return await Task.FromResult(pendingTokens);
//    }

//    /// <summary>
//    /// Register a new worker node
//    /// </summary>
//    public void RegisterWorker(WorkerNode worker)
//    {
//        _workers[worker.Id] = worker;
//        _logger.LogInformation("Registered worker {WorkerId} with capacity {Capacity}", 
//            worker.Id, worker.MaxCapacity);
//    }

//    /// <summary>
//    /// Unregister a worker node
//    /// </summary>
//    public void UnregisterWorker(string workerId)
//    {
//        _workers.TryRemove(workerId, out _);
//        _logger.LogInformation("Unregistered worker {WorkerId}", workerId);
//    }

//    /// <summary>
//    /// Update worker heartbeat
//    /// </summary>
//    public void UpdateWorkerHeartbeat(string workerId)
//    {
//        if (_workers.TryGetValue(workerId, out var worker))
//        {
//            _workers[workerId] = worker with { LastHeartbeat = DateTime.UtcNow };
//        }
//    }

//    private WorkerNode? FindBestWorker(string nodeType)
//    {
//        return _workers.Values
//            .Where(w => w.SupportedNodeTypes.Contains(nodeType))
//            .Where(w => w.CurrentLoad < w.MaxCapacity)
//            .Where(w => DateTime.UtcNow - w.LastHeartbeat < TimeSpan.FromMinutes(2))
//            .OrderBy(w => w.CurrentLoad)
//            .FirstOrDefault();
//    }

//    private async Task ProcessDistributedTokensAsync(BpmnModel model, List<string> trace, CancellationToken cancellationToken)
//    {
//        var maxIterations = 50; // Prevent infinite loops
//        var iteration = 0;

//        while (iteration < maxIterations && !cancellationToken.IsCancellationRequested)
//        {
//            var pendingTokens = await GetPendingTokensAsync(cancellationToken);
//            if (!pendingTokens.Any())
//                break;

//            foreach (var token in pendingTokens)
//            {
//                await ProcessTokenAsync(token, model, trace, cancellationToken);
//            }

//            iteration++;
//            await Task.Delay(100, cancellationToken); // Simulate processing time
//        }
//    }

//    private async Task ProcessTokenAsync(ExecutionToken token, BpmnModel model, List<string> trace, CancellationToken cancellationToken)
//    {
//        _processingTokens[token.Id] = token;

//        try
//        {
//            trace.Add($"ProcessingToken: {token.Id} on {token.AssignedWorker ?? "unassigned"}");

//            // Find current node
//            var currentNode = FindNode(model, token.CurrentNodeId);
//            if (currentNode == null)
//            {
//                trace.Add($"NodeNotFound: {token.CurrentNodeId}");
//                return;
//            }

//            // Process the node
//            await ProcessNodeAsync(currentNode, token, model, trace, cancellationToken);

//            // Update worker load
//            if (token.AssignedWorker != null && _workers.TryGetValue(token.AssignedWorker, out var worker))
//            {
//                _workers[token.AssignedWorker] = worker with { CurrentLoad = Math.Max(0, worker.CurrentLoad - 1) };
//            }
//        }
//        catch (Exception ex)
//        {
//            _logger.LogError(ex, "Error processing token {TokenId}", token.Id);
//            trace.Add($"TokenError: {token.Id} - {ex.Message}");
//        }
//        finally
//        {
//            _processingTokens.TryRemove(token.Id, out _);
//        }
//    }

//    private object? FindNode(BpmnModel model, string nodeId)
//    {
//        return model.Events.FirstOrDefault(e => e.Id == nodeId) as object
//            ?? model.Tasks.FirstOrDefault(t => t.Id == nodeId) as object
//            ?? model.Gateways.FirstOrDefault(g => g.Id == nodeId) as object
//            ?? model.Subprocesses.FirstOrDefault(s => s.Id == nodeId) as object;
//    }

//    private async Task ProcessNodeAsync(object node, ExecutionToken token, BpmnModel model, List<string> trace, CancellationToken cancellationToken)
//    {
//        _processingTokens[token.Id] = token;
//        try
//        {
//            trace.Add($"ProcessingToken: {token.Id} on {token.AssignedWorker ?? "unassigned"}");
//            if (node == null)
//            {
//                trace.Add($"NodeNotFound: {token.CurrentNodeId}");
//                return;
//            }

//            switch (node)
//            {
//                case BpmnEvent evt when evt.Type == "endEvent":
//                    trace.Add($"EndEvent: {evt.Id}");
//                    break;

//                case BpmnTask task:
//                    trace.Add($"DistributedTask: {task.Type} {task.Id} on worker {token.AssignedWorker}");

//                    // SCRIPT TASK: lokal ausführen (Script finnshed quickly)
//                    if (string.Equals(task.Type, "scriptTask", StringComparison.OrdinalIgnoreCase))
//                    {
//                        trace.Add($"ScriptTask-distributed: executing {task.Id} locally");
//                        // Ensure ProcessVariables merged into token.Variables (propagate context)
//                        if (model.ProcessVariables != null)
//                        {
//                            foreach (var kv in model.ProcessVariables)
//                                token.Variables[kv.Key] = kv.Value;
//                        }
//                        // Execute script and copy back variables to model
//                        await ScriptTaskExecution.TryHandleScriptTaskAsync(task, model.ProcessVariables, cancellationToken).ConfigureAwait(false);
//                        // Merge model.ProcessVariables back into token variables for subsequent tokens
//                        if (model.ProcessVariables != null)
//                        {
//                            foreach (var kv in model.ProcessVariables)
//                                token.Variables[kv.Key] = kv.Value;
//                        }

//                        trace.Add($"ScriptTaskCompleted: {task.Id}");
//                        await ContinueToNextNode(task.Id, token, model, trace, cancellationToken).ConfigureAwait(false);
//                        break;
//                    }

//                    // SERVICE TASK: lokal handler oder remote dispatch
//                    if (string.Equals(task.Type, "serviceTask", StringComparison.OrdinalIgnoreCase))
//                    {
//                        trace.Add($"ServiceTask (distributed): {task.Id} impl={task.Implementation}");

//                        // Prepare attributes + variables to send
//                        var attributes = task.Attributes ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
//                        var variables = token.Variables ?? new Dictionary<string, object>();

//                        // 1) Try local handler
//                        if (_serviceRegistry.TryResolve(task.Implementation ?? string.Empty, out var handler))
//                        {
//                            trace.Add($"ServiceTask: local handler found for {task.Implementation}, executing locally");
//                            // Execute local handler
//                            await handler.ExecuteAsync(attributes, variables, cancellationToken).ConfigureAwait(false);
//                            trace.Add($"ServiceTaskCompleted(local): {task.Id}");
//                        }
//                        else
//                        {
//                            // 2) Remote dispatch
//                            var targetWorker = token.AssignedWorker ?? FindBestWorker(task.Type)?.Id;
//                            trace.Add($"ServiceTask: no local handler => dispatch to worker '{targetWorker ?? "any"}'");
//                            await _messageDispatcher.DispatchServiceTaskAsync(targetWorker ?? string.Empty, task.Implementation ?? string.Empty, attributes, variables, cancellationToken).ConfigureAwait(false);
//                            trace.Add($"ServiceTaskDispatched: {task.Id} -> {targetWorker ?? "none"}");
//                        }

//                        // Merge back variables into model.ProcessVariables and token
//                        if (model.ProcessVariables == null)
//                            model = model with { ProcessVariables = new Dictionary<string, object>(variables) };
//                        else
//                        {
//                            foreach (var kv in variables)
//                                model.ProcessVariables[kv.Key] = kv.Value;
//                        }
//                        token = token with { Variables = new Dictionary<string, object>(variables) };

//                        // Continue
//                        await ContinueToNextNode(task.Id, token, model, trace, cancellationToken).ConfigureAwait(false);
//                        break;
//                    }

//                    // other task types: just continue
//                    await ContinueToNextNode(task.Id, token, model, trace, cancellationToken).ConfigureAwait(false);
//                    break;

//                case BpmnGateway gateway:
//                    trace.Add($"DistributedGateway: {gateway.Type} {gateway.Id}");
//                    await ProcessGateway(gateway, token, model, trace, cancellationToken).ConfigureAwait(false);
//                    break;

//                case BpmnEvent evt2:
//                    trace.Add($"DistributedEvent: {evt2.Type} {evt2.Id}");
//                    await ContinueToNextNode(evt2.Id, token, model, trace, cancellationToken).ConfigureAwait(false);
//                    break;
//            }
//        }
//        catch (Exception ex)
//        {
//            _logger.LogError(ex, "Error processing token {TokenId}", token.Id);
//            trace.Add($"TokenError: {token.Id} - {ex.Message}");
//            throw;
//        }
//        finally
//        {
//            _processingTokens.TryRemove(token.Id, out _);
//        }
//    }

//    //private async Task ProcessNodeAsync(object node, ExecutionToken token, BpmnModel model, List<string> trace, CancellationToken cancellationToken)
//    //{
//    //    switch (node)
//    //    {
//    //        case BpmnEvent evt when evt.Type == "endEvent":
//    //            trace.Add($"EndEvent: {evt.Id}");
//    //            break;

//    //        case BpmnTask task:
//    //            trace.Add($"DistributedTask: {task.Type} {task.Id} on worker {token.AssignedWorker}");
//    //            await Task.Delay(50, cancellationToken); // Simulate task execution
//    //            await ContinueToNextNode(task.Id, token, model, trace, cancellationToken);
//    //            break;

//    //        case BpmnGateway gateway:
//    //            trace.Add($"DistributedGateway: {gateway.Type} {gateway.Id}");
//    //            await ProcessGateway(gateway, token, model, trace, cancellationToken);
//    //            break;

//    //        case BpmnEvent evt:
//    //            trace.Add($"DistributedEvent: {evt.Type} {evt.Id}");
//    //            await ContinueToNextNode(evt.Id, token, model, trace, cancellationToken);
//    //            break;
//    //    }
//    //}

//    private async Task ProcessGateway(BpmnGateway gateway, ExecutionToken token, BpmnModel model, List<string> trace, CancellationToken cancellationToken)
//    {
//        var outgoingFlows = model.SequenceFlows.Where(f => f.SourceRef == gateway.Id).ToList();

//        switch (gateway.Type)
//        {
//            case "parallelGateway":
//                // Create tokens for all outgoing flows
//                foreach (var flow in outgoingFlows)
//                {
//                    var newToken = new ExecutionToken(
//                        Guid.NewGuid(),
//                        token.ProcessInstanceId,
//                        flow.TargetRef,
//                        GetNodeType(model, flow.TargetRef),
//                        new Dictionary<string, object>(token.Variables),
//                        DateTime.UtcNow
//                    );
//                    await DistributeTokenAsync(newToken, cancellationToken);
//                    trace.Add($"ParallelBranch: {flow.TargetRef}");
//                }
//                break;

//            case "exclusiveGateway":
//                // Select first flow (simplified)
//                if (outgoingFlows.Any())
//                {
//                    var selectedFlow = outgoingFlows[0];
//                    var newToken = token with 
//                    { 
//                        CurrentNodeId = selectedFlow.TargetRef,
//                        NodeType = GetNodeType(model, selectedFlow.TargetRef)
//                    };
//                    await DistributeTokenAsync(newToken, cancellationToken);
//                    trace.Add($"ExclusiveBranch: {selectedFlow.TargetRef}");
//                }
//                break;
//        }
//    }

//    private async Task ContinueToNextNode(string currentNodeId, ExecutionToken token, BpmnModel model, List<string> trace, CancellationToken cancellationToken)
//    {
//        var outgoingFlows = model.SequenceFlows.Where(f => f.SourceRef == currentNodeId).ToList();

//        foreach (var flow in outgoingFlows)
//        {
//            trace.Add($"SequenceFlow: {flow.Id}");
//            var newToken = token with 
//            { 
//                CurrentNodeId = flow.TargetRef,
//                NodeType = GetNodeType(model, flow.TargetRef)
//            };
//            await DistributeTokenAsync(newToken, cancellationToken);
//        }
//    }

//    private string GetNodeType(BpmnModel model, string nodeId)
//    {
//        if (model.Events.Any(e => e.Id == nodeId))
//            return model.Events.First(e => e.Id == nodeId).Type;
//        if (model.Tasks.Any(t => t.Id == nodeId))
//            return model.Tasks.First(t => t.Id == nodeId).Type;
//        if (model.Gateways.Any(g => g.Id == nodeId))
//            return model.Gateways.First(g => g.Id == nodeId).Type;
//        if (model.Subprocesses.Any(s => s.Id == nodeId))
//            return "subprocess";
//        return "unknown";
//    }

//    private void ProcessHeartbeats(object? state)
//    {
//        var cutoffTime = DateTime.UtcNow.AddMinutes(-2);
//        var deadWorkers = _workers.Values
//            .Where(w => w.LastHeartbeat < cutoffTime)
//            .Select(w => w.Id)
//            .ToList();

//        foreach (var deadWorkerId in deadWorkers)
//        {
//            UnregisterWorker(deadWorkerId);
//        }
//    }

//    public void Dispose()
//    {
//        _heartbeatTimer?.Dispose();
//    }
//}

//public class DistributedTokenEngineAdapter : IProcessEngine
//{
//    private readonly IDistributedTokenEngine _distributed;
//    public DistributedTokenEngineAdapter(IDistributedTokenEngine distributed) { _distributed = distributed; }
//    public List<string> Execute(BpmnModel model)
//    {
//        // Sync-Wrapper für Demo, besser: Async-Interface!
//        return _distributed.ExecuteAsync(model).GetAwaiter().GetResult();
//    }
//}
