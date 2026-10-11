using System.Collections.Concurrent;
using Acornima;
using Jint;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Trace;
using VertexBPMN.Application;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Exceptions;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Domain.Model.Bpmn;
using VertexBPMN.Domain.Model.Cmn;
using VertexBPMN.Infrastructure.Scripting;
using ExecutionToken = VertexBPMN.Domain.Entities.ExecutionToken;

namespace VertexBPMN.Engine.Execution;

public class DistributedProcessEngine : IDistributedProcessEngine, IDisposable
{
	private readonly ILogger<DistributedProcessEngine> _logger;
	private readonly IConfiguration? _configuration;
	private readonly IServiceTaskRegistry _serviceRegistry;
	private readonly IMessageDispatcher _messageDispatcher;
	private readonly IProcessInstanceStore _store;
	private readonly IDmnEngine _dmnEngine;
	private readonly IDmnParser _dmnParser;
	private readonly ICmmnParser _cmmnParser;
	private readonly IBpmnParser _bpmnParser;
	private readonly IAiDecisionService _aiDecisionService;
	private readonly Tracer _tracer;
	private readonly BpmnExecutionComponent _executionComponent = new();
	private readonly ConcurrentDictionary<Guid, CaseToken> _processingCaseTokens = new();
	private readonly ConcurrentDictionary<Guid, ExecutionToken> _processingTokens = new();
	private readonly Timer _heartbeatTimer;

	public DistributedProcessEngine(
		ILogger<DistributedProcessEngine> logger,
		IServiceTaskRegistry serviceRegistry,
		IMessageDispatcher dispatcher,
		IProcessInstanceStore store,
		IDmnEngine dmnEngine,
		IDmnParser dmnParser,
		ICmmnParser cmmnParser,
		IBpmnParser bpmnParser,
		IAiDecisionService aiDecisionService,
		TracerProvider tracerProvider,
		IConfiguration? configuration = null)
	{
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_configuration = configuration;
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
			[
				"userTask", "serviceTask", "mcpServiceTask", "scriptTask", "businessRuleTask", "subprocess",
				"humanTask", "caseTask", "adHocSubprocess", "eventListener"
			],
			0,
			10,
			SupportsDmn: true,
			SupportsCmmn: true,
			SupportsBpmn: true
		);
		_store.SaveWorkerAsync(currentWorker).GetAwaiter().GetResult();

		_heartbeatTimer = new Timer(static state => { _ = ((DistributedProcessEngine)state!).ProcessHeartbeatsAsync(); }, this, TimeSpan.FromSeconds(10),
			TimeSpan.FromSeconds(10));
	}

	public async Task<List<string>> ExecuteAsync(BpmnModel model, CancellationToken cancellationToken = default)
	{
		VertexBPMN.Engine.Parsing.ExternalTaskValidation.RejectUnsupported(model);
		await ValidateCalledModelsAsync(model, cancellationToken);
		return await ExecuteValidatedModelAsync(model, cancellationToken);
	}

	private async Task ValidateCalledModelsAsync(BpmnModel model, CancellationToken cancellationToken)
	{
		var pendingModels = new Stack<BpmnModel>();
		var visitedModels = new HashSet<string>(StringComparer.Ordinal);
		pendingModels.Push(model);
		while (pendingModels.TryPop(out var currentModel))
		{
			if (!visitedModels.Add(currentModel.ProcessId))
			{
				continue;
			}

			if (visitedModels.Count > 64)
			{
				throw new InvalidOperationException("external_task_call_graph_limit");
			}

			VertexBPMN.Engine.Parsing.ExternalTaskValidation.RejectUnsupported(currentModel);
			foreach (var call in (currentModel.Tasks ?? []).Where(task => task.Type == "callActivity"))
			{
				if (call.Attributes?.GetValueOrDefault("calledElement") is { Length: > 0 } key)
				{
					cancellationToken.ThrowIfCancellationRequested();
					var xml = await _store.GetBpmnModelAsync(key);
					if (!string.IsNullOrWhiteSpace(xml))
					{
						pendingModels.Push(await _bpmnParser.ParseAsync(xml, cancellationToken));
					}
				}
			}
		}
	}

	private async Task<List<string>> ExecuteValidatedModelAsync(BpmnModel model, CancellationToken cancellationToken)
	{
		var trace = new List<string>();
		var processInstanceId = Guid.NewGuid();
		try
		{
			var startEvent = (model.Events ?? []).FirstOrDefault(e => e.Type == "startEvent") ??
							 throw new BpmnEngineException("No startEvent found");
			trace.Add($"DistributedExecution: Starting process: {processInstanceId}, StartEvent: {startEvent.Id}");

			var nextIds = (model.SequenceFlows ?? []).Where(f => f.SourceRef == startEvent.Id).Select(f => f.TargetRef)
				.ToList();
			foreach (var id in nextIds)
			{
				var token = new ExecutionToken(Guid.NewGuid(), processInstanceId, id, "start",
					new Dictionary<string, object>(model.ProcessVariables ?? new Dictionary<string, object>(StringComparer.Ordinal), StringComparer.Ordinal),
					DateTime.UtcNow)
				{
					ProcessInstanceId = processInstanceId
				};
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

	public List<string> Execute(BpmnModel model)
	{
		return ExecuteAsync(model, CancellationToken.None).GetAwaiter().GetResult();
	}

	public async Task<List<string>> ExecuteProcessAsync(string processId,
		CancellationToken cancellationToken = default)
	{
		var trace = new List<string>();
		var bpmnXml = await _store.GetBpmnModelAsync(processId);
		var processModel = await _bpmnParser.ParseAsync(bpmnXml, cancellationToken);
		var token = new ExecutionToken(
			Guid.NewGuid(),
			Guid.Parse(processModel.Id),
			(processModel.Events ?? []).FirstOrDefault(pi =>
				pi.Type == "eventListener" && pi.Definitions?.SingleOrDefault()?.Kind == "startEvent")?.Id
			?? throw new DistributedTokenException("No start event found in process"),
			"eventListener",
			processModel.ProcessVariables is null
				? new Dictionary<string, object>(StringComparer.Ordinal)
				: new Dictionary<string, object>(processModel.ProcessVariables, StringComparer.Ordinal),
			DateTime.UtcNow
		);

		await _store.SaveTokenAsync(token);
		await DistributeTokenAsync(token, cancellationToken);
		return trace;
	}

	public async Task<CaseModel> GetCmmnModelAsync(string caseId)
	{
		var xml = await _store.GetCmmnModelAsync(caseId);
		return await _cmmnParser.ParseAsync(xml);
	}

	public Task<List<HistoricalCaseData>> GetHistoricalCaseDataAsync(string caseId)
	{
		return _store.GetHistoricalCaseDataAsync(caseId);
	}

	public async Task<List<string>> ExecuteCaseAsync(CaseModel model, CancellationToken cancellationToken = default)
	{
		var trace = new List<string>();
		var caseInstanceId = Guid.NewGuid();

		try
		{
			trace.Add($"DistributedCaseExecution: Starting case {caseInstanceId}");
			var initialCaseFile =
				model.CaseFileItems.ToDictionary(item => item.Id, item => item.Value ?? new object(), StringComparer.Ordinal);

			foreach (var planItem in model.PlanItems.Where(pi =>
						 pi.EntrySentryRefs == null || pi.EntrySentryRefs.Count == 0))
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
			await _store.SaveHistoricalCaseDataAsync(new HistoricalCaseData(
				model.Id,
				new Dictionary<string, object>(initialCaseFile, StringComparer.Ordinal),
				[.. trace],
				DateTime.UtcNow));
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
		return workers.Any(w =>
			w.CurrentLoad < w.MaxCapacity && DateTime.UtcNow - w.LastHeartbeat < TimeSpan.FromMinutes(2));
	}

	public async Task DistributeTokenAsync(
		ExecutionToken token,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(token);

		if (IsTerminalExecutionToken(token))
		{
			if (_logger.IsEnabled(Microsoft.Extensions.Logging.LogLevel.Debug))
			{
				_logger.LogDebug(
					"Token {TokenId} is terminal with state {State}; " +
					"distribution is skipped.",
					token.Id,
					token.State);
			}

			return;
		}

		try
		{
			var bestWorker = await FindBestWorkerAsync(token.NodeType);

			var assignedToken = CreateAssignedToken(token, bestWorker);

			await _store.SaveTokenAsync(assignedToken);

			await _messageDispatcher.PublishTokenAsync(
				assignedToken,
				cancellationToken);

			if (_logger.IsEnabled(Microsoft.Extensions.Logging.LogLevel.Information))
			{
				_logger.LogInformation(
					"Token {TokenId} assigned to worker {WorkerId} " +
					"with state {State}",
					assignedToken.Id,
					assignedToken.AssignedWorker ?? "none",
					assignedToken.State);
			}
		}
		catch (OperationCanceledException)
			when (cancellationToken.IsCancellationRequested)
		{
			throw;
		}
		catch (Exception ex)
		{
			_logger.LogError(
				ex,
				"Failed to distribute token {TokenId}",
				token.Id);

			throw new DistributedTokenException(
				$"Failed to distribute token {token.Id}",
				ex);
		}
	}

	private static bool IsTerminalExecutionToken(ExecutionToken token) =>
		string.Equals(token.State, ExecutionToken.CompletedState, StringComparison.OrdinalIgnoreCase)
		|| string.Equals(token.State, ExecutionToken.FailedState, StringComparison.OrdinalIgnoreCase);

	private static ExecutionToken CreateAssignedToken(ExecutionToken token, WorkerNode? worker)
	{
		return new ExecutionToken
		{
			Id = token.Id,
			ProcessInstanceId = token.ProcessInstanceId,
			CurrentNodeId = token.CurrentNodeId,
			NodeType = token.NodeType,
			Variables = token.Variables != null
					? new Dictionary<string, object>(token.Variables, StringComparer.Ordinal)
					: new Dictionary<string, object>(StringComparer.Ordinal),
			CreatedAt = token.CreatedAt,
			AssignedWorker = worker?.Id,
			AssignedAt = worker == null
					? null
					: DateTime.UtcNow,

			/*
				 * RetryCount gehört zur Ausführung des aktuellen Knotens.
				 * Beim Weiterleiten auf den nächsten Knoten beginnt die
				 * Retry-Zählung daher wieder bei null.
				 */
			RetryCount = 0,

			State = ExecutionToken.PendingState
		};
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
			if (_logger.IsEnabled(LogLevel.Information))
			{
				_logger.LogInformation("CaseToken {TokenId} assigned to worker {WorkerId}", token.Id,
								bestWorker?.Id ?? "none");
			}
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
			if (_logger.IsEnabled(LogLevel.Information))
			{
				_logger.LogInformation("Registered worker {WorkerId} with capacity {Capacity}", worker.Id,
								worker.MaxCapacity);
			}
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
			if (_logger.IsEnabled(LogLevel.Information))
			{
				_logger.LogInformation("Unregistered worker {WorkerId}", workerId);
			}
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
			var decision = await _dmnParser.ParseAsync(dmnXml);
			if (!string.IsNullOrWhiteSpace(decision.SourceXml))
			{
				_ = DmnDecisionGraph.Parse(decision.SourceXml, decisionId);
			}

			await _store.SaveDmnModelAsync(decisionId, dmnXml);
			if (_logger.IsEnabled(LogLevel.Information))
			{
				_logger.LogInformation("Registered DMN model {DecisionId}", decisionId);
			}
		}
		catch (Exception ex) when (ex is DmnParseException or InvalidOperationException)
		{
			_logger.LogError(ex, "Invalid DMN XML for decision {DecisionId}", decisionId);
			throw new DistributedTokenException($"Invalid DMN XML for decision {decisionId}", ex);
		}
	}

	public async Task RegisterBpmnModelAsync(string processId, string bpmnXml,
		CancellationToken cancellationToken = default)
	{
		await _store.SaveBpmnModelAsync(processId, bpmnXml);
		if (_logger.IsEnabled(LogLevel.Information))
		{
			_logger.LogInformation("Registered BPMN model {ProcessId}", processId);
		}
	}

	public async Task RegisterCmmnModelAsync(string caseId, string cmmnXml)
	{
		try
		{
			await _cmmnParser.ParseAsync(cmmnXml);
			await _store.SaveCmmnModelAsync(caseId, cmmnXml);
			if (_logger.IsEnabled(LogLevel.Information))
			{
				_logger.LogInformation("Registered CMMN model {CaseId}", caseId);
			}
		}
		catch (CmmnParseException ex)
		{
			_logger.LogError(ex, "Invalid CMMN XML for case {CaseId}", caseId);
			throw new DistributedTokenException($"Invalid CMMN XML for case {caseId}", ex);
		}
	}

	public async Task AddDiscretionaryItemAsync(string caseId, PlanItem planItem,
		CancellationToken cancellationToken = default)
	{
		try
		{
			if (!planItem.IsDiscretionary)
			{
				throw new DistributedTokenException("PlanItem must be discretionary");
			}

			var cmmnXml = await _store.GetCmmnModelAsync(caseId)
						  ?? throw new DistributedTokenException($"CMMN model {caseId} not found");
			var caseModel = await _cmmnParser.ParseAsync(cmmnXml, cancellationToken);

			var updatedPlanItems = new List<PlanItem>(caseModel.PlanItems) { planItem };
			var updatedModel = caseModel with { PlanItems = updatedPlanItems };
			await _store.UpdateCaseModelAsync(updatedModel);

			var caseTokens = await _store.GetPendingCaseTokensAsync();
			var caseToken = caseTokens.FirstOrDefault(t => t.CaseInstanceId == Guid.Parse(caseId));
			if (caseToken.Id != Guid.Empty)
			{
				var newToken = new CaseToken(
					Guid.NewGuid(),
					caseToken.CaseInstanceId,
					planItem.Id,
					planItem.Type,
					new Dictionary<string, object>(caseToken.CaseFile, StringComparer.Ordinal),
					DateTime.UtcNow
				);
				await DistributeCaseTokenAsync(newToken, cancellationToken);
				if (_logger.IsEnabled(LogLevel.Information))
				{
					_logger.LogInformation("Discretionary item {PlanItemId} added to case {CaseId}", planItem.Id,
										caseId);
				}
			}
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to add discretionary item to case {CaseId}", caseId);
			throw new DistributedTokenException($"Failed to add discretionary item to case {caseId}", ex);
		}
	}

	public async Task UpdateCaseFileItemAsync(string caseId, string caseFileItemId, object newValue,
		CancellationToken cancellationToken = default)
	{
		using var span = _tracer.StartActiveSpan("UpdateCaseFileItem");
		span.SetAttribute("caseId", caseId);
		span.SetAttribute("caseFileItemId", caseFileItemId);
		try
		{
			var cmmnXml = await _store.GetCmmnModelAsync(caseId)
						  ?? throw new DistributedTokenException($"CMMN model {caseId} not found");
			var caseModel = await _cmmnParser.ParseAsync(cmmnXml, cancellationToken);

			_ = caseModel.CaseFileItems.FirstOrDefault(cfi => cfi.Id == caseFileItemId)
							   ?? throw new DistributedTokenException($"CaseFileItem {caseFileItemId} not found");
			var updatedCaseFileItems = caseModel.CaseFileItems
				.Select(cfi => cfi.Id == caseFileItemId ? cfi with { Value = newValue } : cfi)
				.ToList();
			var updatedModel = caseModel with { CaseFileItems = updatedCaseFileItems };
			await _store.UpdateCaseModelAsync(updatedModel);

			var updateEvent = new CaseFileUpdateEvent(caseId, caseFileItemId, newValue, DateTime.UtcNow);
			await _messageDispatcher.PublishCaseFileUpdateAsync(updateEvent, cancellationToken);
			if (_logger.IsEnabled(LogLevel.Information))
			{
				_logger.LogInformation("CaseFileItem {CaseFileItemId} updated in case {CaseId}", caseFileItemId,
								caseId);
			}
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
						CaseFile = new Dictionary<string, object>(token.CaseFile, StringComparer.Ordinal) { [caseFileItemId] = newValue }
					};
					await ProcessCaseTokenAsync(newToken, caseModel, [], cancellationToken);
				}
			}
		}
		catch (Exception ex)
		{
			span.SetStatus(Status.Error.WithDescription(ex.Message));
			_logger.LogError(ex, "Failed to update CaseFileItem {CaseFileItemId} in case {CaseId}", caseFileItemId,
				caseId);
			throw new DistributedTokenException($"Failed to update CaseFileItem {caseFileItemId} in case {caseId}",
				ex);
		}
	}

	public async Task TriggerUserEventAsync(string caseId, string eventId, Dictionary<string, object> eventData,
		CancellationToken cancellationToken = default)
	{
		try
		{
			var cmmnXml = await _store.GetCmmnModelAsync(caseId)
						  ?? throw new DistributedTokenException($"CMMN model {caseId} not found");
			var caseModel = await _cmmnParser.ParseAsync(cmmnXml, cancellationToken);

			var planItem = caseModel.PlanItems.FirstOrDefault(pi =>
							   pi.Id == eventId && pi.Type == "eventListener" &&
							   pi.DefinitionRef == "userEventListener")
						   ?? throw new DistributedTokenException($"UserEventListener {eventId} not found");

			var caseTokens = await _store.GetPendingCaseTokensAsync();
			var caseToken = caseTokens.FirstOrDefault(t => t.CaseInstanceId == Guid.Parse(caseId));
			if (caseToken.Id != Guid.Empty)
			{
				var updatedCaseFile = new Dictionary<string, object>(caseToken.CaseFile, StringComparer.Ordinal);
				foreach (var kvp in eventData)
				{
					updatedCaseFile[kvp.Key] = kvp.Value;
				}

				var newToken = new CaseToken(
					Guid.NewGuid(),
					caseToken.CaseInstanceId,
					planItem.Id,
					planItem.Type,
					updatedCaseFile,
					DateTime.UtcNow
				);
				await DistributeCaseTokenAsync(newToken, cancellationToken);
				if (_logger.IsEnabled(LogLevel.Information))
				{
					_logger.LogInformation("Triggered user event {EventId} for case {CaseId}", eventId, caseId);
				}
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
			var caseModel = await _cmmnParser.ParseAsync(cmmnXml, cancellationToken);

			var caseTokens = await _store.GetPendingCaseTokensAsync();
			var caseToken = caseTokens.FirstOrDefault(t => t.CaseInstanceId == Guid.Parse(caseId));
			if (caseToken.Id == Guid.Empty)
			{
				throw new DistributedTokenException($"No active tokens found for case {caseId}");
			}

			// Prädiktive Optimierung mit externem Kontext
			await AddPredictedOrGeneratedPlanItemsAsync(caseId, caseToken, cancellationToken);

			// Speichere historische Daten
			var completedPlanItems = caseModel.PlanItems.Where(pi => pi.Type != "eventListener").Select(pi => pi.Id)
				.ToList();
			var historicalDataEntry =
				new HistoricalCaseData(caseId, caseToken.CaseFile, completedPlanItems, DateTime.UtcNow);
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
	private async Task AddPredictedOrGeneratedPlanItemsAsync(string caseId, CaseToken caseToken, CancellationToken cancellationToken)
	{
		var historicalData = await _store.GetHistoricalCaseDataAsync(caseId);
		var predictedPlanItems =
			await _aiDecisionService.PredictOptimalPlanItemsAsync(caseId, caseToken.CaseFile, historicalData,
				cancellationToken);

		foreach (var planItem in predictedPlanItems)
		{
			await AddDiscretionaryItemAsync(caseId, planItem with { IsDiscretionary = true }, cancellationToken);
			if (_logger.IsEnabled(LogLevel.Information))
			{
				_logger.LogInformation("Added AI-predicted PlanItem {PlanItemId} to case {CaseId}", planItem.Id,
									caseId);
			}
		}

		// Fallback auf Ad-hoc-Subprozess, falls keine prädiktiven Vorschläge
		if (predictedPlanItems.Count == 0)
		{
			var adHocSubprocess =
				await _aiDecisionService.GenerateAdHocSubprocessAsync(caseId, caseToken.CaseFile,
					cancellationToken);
			await AddDiscretionaryItemAsync(caseId, adHocSubprocess with { IsDiscretionary = true },
				cancellationToken);
			if (_logger.IsEnabled(LogLevel.Information))
			{
				_logger.LogInformation("Added AI-generated ad-hoc subprocess {PlanItemId} to case {CaseId}",
									adHocSubprocess.Id, caseId);
			}
		}


	}


	private async Task<WorkerNode?> FindBestWorkerAsync(string nodeType)
	{
		var workers = await _store.GetActiveWorkersAsync() ?? [];
		return workers
			.Where(worker => SupportsNodeType(worker, nodeType)
				&& worker.CurrentLoad < worker.MaxCapacity
				&& DateTime.UtcNow - worker.LastHeartbeat < TimeSpan.FromMinutes(2))
			.OrderBy(worker => worker.CurrentLoad)
			.FirstOrDefault();
	}

	private static bool SupportsNodeType(WorkerNode worker, string nodeType) => nodeType switch
	{
		"businessRuleTask" => worker.SupportsDmn,
		"humanTask" or "caseTask" => worker.SupportsCmmn,
		_ => worker.SupportedNodeTypes.Contains(nodeType)
	};

	private async Task ProcessDistributedTokensAsync(BpmnModel model, List<string> trace,
		CancellationToken cancellationToken)
	{
		const int maxIterations = 50;
		var iteration = 0;

		while (iteration < maxIterations && !cancellationToken.IsCancellationRequested)
		{
			var pendingTokens = await GetPendingTokensAsync(cancellationToken);
			if (pendingTokens.Count == 0)
			{
				break;
			}

			foreach (var token in pendingTokens)
			{
				await ProcessTokenAsync(token, model, trace, cancellationToken);
			}

			iteration++;
			await Task.Delay(100, cancellationToken);
		}
	}

	private async Task ProcessDistributedCaseTokensAsync(CaseModel model, List<string> trace,
		CancellationToken cancellationToken)
	{
		const int maxIterations = 50;
		var iteration = 0;

		while (iteration < maxIterations && !cancellationToken.IsCancellationRequested)
		{
			var pendingTokens = await GetPendingCaseTokensAsync(cancellationToken);
			if (pendingTokens.Count == 0)
			{
				break;
			}

			await Parallel.ForEachAsync(pendingTokens, cancellationToken,
				async (token, ct) => { await ProcessCaseTokenAsync(token, model, trace, ct); });

			iteration++;
			await Task.Delay(100, cancellationToken);
		}
	}

	private async Task ProcessTokenAsync(ExecutionToken token, BpmnModel model, List<string> trace,
		CancellationToken cancellationToken)
	{
		if (!await TryBeginTokenProcessingAsync(token, trace))
		{
			return;
		}

		try
		{
			await ExecuteTokenWithRetriesAsync(token, model, trace, cancellationToken);
		}
		finally
		{
			_processingTokens.TryRemove(token.Id, out _);
		}
	}

	private async Task<bool> TryBeginTokenProcessingAsync(ExecutionToken token, List<string> trace)
	{
		if (string.Equals(token.State, ExecutionToken.CompletedState, StringComparison.OrdinalIgnoreCase) ||
			string.Equals(token.State, ExecutionToken.FailedState, StringComparison.OrdinalIgnoreCase))
		{
			trace.Add($"TokenSkipped: {token.Id} has terminal state '{token.State}'");
			return false;
		}

		if (string.IsNullOrWhiteSpace(token.State))
		{
			token.SetState(ExecutionToken.PendingState);
			await _store.SaveTokenAsync(token);
		}

		if (!string.Equals(token.State, ExecutionToken.PendingState, StringComparison.OrdinalIgnoreCase))
		{
			trace.Add($"TokenSkipped: {token.Id} has non-executable state '{token.State}'");
			return false;
		}

		if (!_processingTokens.TryAdd(token.Id, token))
		{
			trace.Add($"TokenSkipped: {token.Id} is already being processed");
			return false;
		}

		return true;
	}

	private async Task ExecuteTokenWithRetriesAsync(ExecutionToken token, BpmnModel model, List<string> trace,
		CancellationToken cancellationToken)
	{
		while (true)
		{
			try
			{
				await DispatchCurrentNodeAsync(token, model, trace, cancellationToken);
				await ReleaseAssignedWorkerLoadAsync(token);
				return;
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
				token.SetState(ExecutionToken.PendingState);
				await _store.SaveTokenAsync(token);
				throw;
			}
			catch (Exception ex)
			{
				if (await HandleTokenFailureAsync(token, trace, ex, cancellationToken))
				{
					return;
				}
			}
		}
	}

	private Task DispatchCurrentNodeAsync(ExecutionToken token, BpmnModel model, List<string> trace,
		CancellationToken cancellationToken)
	{
		var currentNode = FindNode(model, token.CurrentNodeId)
			?? throw new DistributedTokenException($"Node '{token.CurrentNodeId}' for token '{token.Id}' was not found.");
		return currentNode switch
		{
			BpmnEvent evt => ProcessEventAsync(evt, token, model, trace, cancellationToken),
			BpmnTask task => ProcessTaskAsync(task, token, model, trace, cancellationToken),
			BpmnGateway gateway => ProcessGatewayAsync(gateway, token, model, trace, cancellationToken),
			BpmnSubprocess subprocess => ProcessSubprocessAsync(subprocess, token, model, trace, cancellationToken),
			_ => throw new DistributedTokenException($"Unsupported node type '{currentNode.GetType().Name}' for token '{token.Id}'.")
		};
	}

	private async Task ReleaseAssignedWorkerLoadAsync(ExecutionToken token)
	{
		if (token.AssignedWorker is null)
		{
			return;
		}

		try
		{
			var worker = await _store.GetWorkerAsync(token.AssignedWorker);
			if (worker is not null)
			{
				await _store.SaveWorkerAsync(worker with { CurrentLoad = Math.Max(0, worker.CurrentLoad - 1) });
			}
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "Could not update load for worker {WorkerId} after processing token {TokenId}.",
				token.AssignedWorker, token.Id);
		}
	}

	private async Task<bool> HandleTokenFailureAsync(ExecutionToken token, List<string> trace, Exception exception,
		CancellationToken cancellationToken)
	{
		const int maxRetries = 3;
		token.IncrementRetry();
		var isTransient = IsTransientTokenFailure(exception);
		if (!isTransient || token.RetryCount >= maxRetries)
		{
			token.SetState(ExecutionToken.FailedState);
			await _store.SaveTokenAsync(token);
			if (isTransient)
			{
				_logger.LogError(exception, "Max retries reached for token {TokenId}", token.Id);
			}
			else
			{
				_logger.LogError(exception, "Non-transient failure for token {TokenId}", token.Id);
			}
			await _store.SaveToDeadLetterQueueAsync(token, exception.Message);
			trace.Add($"TokenFailed: {token.Id} - {exception.Message}");
			return true;
		}

		token.SetState(ExecutionToken.PendingState);
		await _store.SaveTokenAsync(token);
		_logger.LogWarning(exception, "Retry {RetryCount}/{MaxRetries} for token {TokenId}",
			token.RetryCount, maxRetries, token.Id);
		await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, token.RetryCount)), cancellationToken);
		return false;
	}

	private static bool IsTransientTokenFailure(Exception exception)
	{
		return exception switch
		{
			TimeoutException => true,
			IOException => true,
			HttpRequestException => true,
			DistributedTokenException => false,
			ArgumentException => false,
			InvalidOperationException => false,
			NotSupportedException => false,
			_ => true
		};
	}

	private async Task ProcessCaseTokenAsync(CaseToken token, CaseModel model, List<string> trace,
		CancellationToken cancellationToken)
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
							   ?? throw new DistributedTokenException(
								   $"PlanItem {token.CurrentPlanItemId} not found");

				if (!await EvaluateSentriesAsync(planItem.EntrySentryRefs, model, token.CaseFile,
						cancellationToken))
				{
					trace.Add($"CaseTokenBlocked: {token.Id} - Entry sentries not satisfied for {planItem.Id}");
					span.SetStatus(Status.Ok);
					return;
				}

				await ExecutePlanItemAsync(planItem, token, model, trace, cancellationToken);

				if (await EvaluateSentriesAsync(planItem.ExitSentryRefs, model, token.CaseFile, cancellationToken))
				{
					trace.Add($"ExitSentryTriggered: {planItem.Id}");
					await CompletePlanItemAsync(planItem, token, model, trace, cancellationToken);
				}

				await ReleaseCaseWorkerLoadAsync(token);

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
					span.SetStatus(Status.Error.WithDescription(ex.Message));
					return;
				}

				await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, retryCount)), cancellationToken);
				_logger.LogWarning(ex, "Retry {RetryCount}/{MaxRetries} for case token {TokenId}", retryCount,
					maxRetries, token.Id);
			}
			finally
			{
				_processingCaseTokens.TryRemove(token.Id, out _);
			}
		}
	}

	private async Task ReleaseCaseWorkerLoadAsync(CaseToken token)
	{
		if (token.AssignedWorker is null)
		{
			return;
		}
		var worker = await _store.GetWorkerAsync(token.AssignedWorker);
		if (worker is not null)
		{
			await _store.SaveWorkerAsync(worker with { CurrentLoad = Math.Max(0, worker.CurrentLoad - 1) });
		}
	}

	private async Task ExecutePlanItemAsync(PlanItem planItem, CaseToken token, CaseModel model,
		List<string> trace, CancellationToken cancellationToken)
	{
		switch (planItem.Type.ToLowerInvariant())
		{
			case "servicetask" when planItem.Attributes?.ContainsKey("type") == true:
				var serviceTaskType = planItem.Attributes["type"];
				var handler = _serviceRegistry.GetHandler(serviceTaskType);
				await handler.ExecuteAsync(planItem.Attributes ?? new Dictionary<string, string>(StringComparer.Ordinal),
					token.CaseFile, cancellationToken);
				trace.Add($"ServiceTaskExecuted: {planItem.Id} (type: {serviceTaskType})");
				await CompletePlanItemAsync(planItem, token, model, trace, cancellationToken);
				break;
			case "adhocsubprocess":
				await ProcessAdHocPlanItemAsync(planItem, token, model, trace, cancellationToken);
				break;
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
				var duration = planItem.Attributes?.GetValueOrDefault("timeDuration") ?? "PT1M";
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
			default:
				trace.Add($"UnsupportedPlanItem: {planItem.Type} {planItem.Id}");
				await CompletePlanItemAsync(planItem, token, model, trace, cancellationToken);
				break;
		}

	}

	private async Task ProcessAdHocPlanItemAsync(PlanItem planItem, CaseToken token, CaseModel model,
		List<string> trace, CancellationToken cancellationToken)
	{
		trace.Add($"AdHocSubprocess: {planItem.Id} started");
		// Dynamische Logik basierend auf AI-generierten Attributen
		var subTasks = planItem.Attributes?.GetValueOrDefault("subTasks", "").Split(';')
			.Select(id => new PlanItem(
				$"subtask_{id}", "humanTask", "humanTaskDef", new(StringComparer.Ordinal) { { "camunda:assignee", "user1" } },
				null, null, true
			)).ToList() ?? [];
		// MCP-Aktion für externe Systeme
		if (planItem.Attributes?.ContainsKey("mcpAction") == true)
		{
			var mcpServerUrl = planItem.Attributes.GetValueOrDefault("mcpServerUrl");
			if (string.IsNullOrWhiteSpace(mcpServerUrl))
			{
				throw new DistributedTokenException($"PlanItem '{planItem.Id}' requires an explicit mcpServerUrl for its MCP action.");
			}
			var mcpMethod = planItem.Attributes["mcpAction"];
			var mcpParams = new Dictionary<string, object>(StringComparer.Ordinal)
			{
				{"caseId", model.Id},
				{"planItemId", planItem.Id}
			};
			await _aiDecisionService.ExecuteMcpActionAsync(model.Id, mcpServerUrl, mcpMethod,
				mcpParams, cancellationToken);
			trace.Add($"MCPActionTriggered: {mcpMethod} on {mcpServerUrl}");
		}

		foreach (var subTask in subTasks)
		{
			await AddDiscretionaryItemAsync(model.Id, subTask, cancellationToken);
			trace.Add($"AdHocSubprocessTaskAdded: {subTask.Id}");
		}

		await CompletePlanItemAsync(planItem, token, model, trace, cancellationToken);
	}

	private async Task ProcessEventAsync(
		BpmnEvent evt,
		ExecutionToken token,
		BpmnModel model,
		List<string> trace,
		CancellationToken cancellationToken)
	{
		var definitions = evt.Definitions
						  ?? [];

		if (definitions.Count > 1)
		{
			throw new DistributedTokenException(
				$"Event '{evt.Id}' contains {definitions.Count} " +
				"event definitions. Exactly one event definition is allowed.");
		}

		var definition = definitions.Count == 0
			? null
			: definitions[0];

		if (definition == null)
		{
			await ProcessNoneEventInlineAsync(
				evt,
				token,
				model,
				trace,
				cancellationToken);

			return;
		}

		await ProcessDefinedEventAsync(
			evt,
			definition,
			token,
			model,
			trace,
			cancellationToken);
	}

	private async Task ProcessDefinedEventAsync(BpmnEvent evt, EventDefinition definition, ExecutionToken token,
		BpmnModel model, List<string> trace, CancellationToken cancellationToken)
	{
		if (definition is MessageEventDefinition messageDefinition)
		{
			await ProcessMessageCatchEventAsync(evt, messageDefinition, token, model, trace, cancellationToken);
			return;
		}
		// Defined events must not silently fall back to ordinary sequence-flow execution.
		throw definition switch
		{
			TimerEventDefinition => new DistributedTokenException($"Timer event '{evt.Id}' is not yet implemented with a persistent waiting state."),
			SignalEventDefinition => new DistributedTokenException("Signal subscription handling is not implemented."),
			_ => new DistributedTokenException($"Event definition '{definition.Kind}' for event '{evt.Id}' is not implemented."),
		};
	}

	private sealed class MessageResumeClaim
	{
		private int _claimed;
		public bool IsClaimed => Interlocked.CompareExchange(ref _claimed, 0, 0) != 0;
		public bool TryClaim() => Interlocked.CompareExchange(ref _claimed, 1, 0) == 0;
	}

	private async Task ProcessMessageCatchEventAsync(BpmnEvent evt, MessageEventDefinition definition, ExecutionToken token,
		BpmnModel model, List<string> trace, CancellationToken cancellationToken)
	{
		if (string.IsNullOrWhiteSpace(definition.MessageRef))
		{
			throw new DistributedTokenException($"Message event '{evt.Id}' has no message reference.");
		}
		token.SetState(ExecutionToken.WaitingState);
		await _store.SaveTokenAsync(token);
		trace.Add($"MessageEventWaiting: {evt.Id} for message {definition.MessageRef}");
		var claim = new MessageResumeClaim();
		await _messageDispatcher.SubscribeToMessageAsync(definition.MessageRef,
			message => ResumeSubscribedMessageAsync(evt, definition, token, model, trace, claim, message, cancellationToken),
			cancellationToken);
	}

	private async Task ResumeSubscribedMessageAsync(BpmnEvent evt, MessageEventDefinition definition, ExecutionToken token,
		BpmnModel model, List<string> trace, MessageResumeClaim claim, Message message, CancellationToken cancellationToken)
	{
		try
		{
			if (claim.IsClaimed)
			{
				return;
			}
			var waitingToken = await _store.GetTokenAsync(token.Id);
			if (!string.Equals(waitingToken.State, ExecutionToken.WaitingState, StringComparison.OrdinalIgnoreCase)
				|| !string.Equals(waitingToken.CurrentNodeId, evt.Id, StringComparison.Ordinal)
				|| !MatchesCorrelation(definition.CorrelationKey, waitingToken, message))
			{
				return;
			}
			// Claim only after every persisted-state and correlation check succeeds.
			if (!claim.TryClaim())
			{
				return;
			}
			var variables = message.Variables ?? new Dictionary<string, object>(StringComparer.Ordinal);
			foreach (var variable in variables)
			{
				waitingToken.Variables[variable.Key] = variable.Value;
			}
			waitingToken.SetState(ExecutionToken.PendingState);
			await _store.SaveTokenAsync(waitingToken);
			lock (trace)
			{
				trace.Add($"MessageReceived: {evt.Id} with message {message.Name}");
			}
			await ContinueToNextNodeAsync(evt.Id, waitingToken, model, trace, cancellationToken);
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			// The subscriber's owner is ending this subscription.
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to resume token {TokenId} after message {MessageName}", token.Id, message.Name);
		}
	}


	private async Task ProcessNoneEventInlineAsync(BpmnEvent evt, ExecutionToken token, BpmnModel model,
		List<string> trace, CancellationToken cancellationToken)
	{
		switch (evt.Type)
		{
			case "startEvent":
				trace.Add($"StartEvent: {evt.Id}");

				await ContinueToNextNodeAsync(
					evt.Id,
					token,
					model,
					trace,
					cancellationToken);

				return;

			case "intermediateCatchEvent"
when evt.Definitions?.Count == 1 &&
	 evt.Definitions[0] is MessageEventDefinition messageDefinition:
				await ProcessMessageCatchEventAsync(evt, messageDefinition, token, model, trace, cancellationToken);
				break;

			case "intermediateThrowEvent":
				trace.Add(
					$"IntermediateThrowEvent: {evt.Id}");

				await ContinueToNextNodeAsync(
					evt.Id,
					token,
					model,
					trace,
					cancellationToken);

				return;

			case "endEvent":
				token.SetState(ExecutionToken.CompletedState);
				await _store.SaveTokenAsync(token);

				trace.Add($"EndEvent: {evt.Id}");
				trace.Add($"EndEventCompleted: {evt.Id} with token {token.Id}");
				return;

			case "boundaryEvent":
				throw new DistributedTokenException(
					$"Boundary event '{evt.Id}' has no event definition.");

			default:
				throw new DistributedTokenException(
					$"Unsupported BPMN event type '{evt.Type}' " +
					$"for event '{evt.Id}'.");
		}
	}

	private async Task ProcessTaskAsync(BpmnTask task, ExecutionToken token, BpmnModel model, List<string> trace,
		CancellationToken cancellationToken)
	{
		trace.Add($"DistributedTask: {task.Type} {task.Id} on worker {token.AssignedWorker}");
		var isAsync = (task.Attributes?.ContainsKey("camunda:async") == true &&
					   task.Attributes["camunda:async"] == "true") ||
					   (task.Attributes?.ContainsKey("flowable:async") == true &&
					   task.Attributes["flowable:async"] == "true");

		if (isAsync)
		{
			trace.Add($"AsyncTask: {task.Id} queued for async processing");
			await _messageDispatcher.QueueTaskAsync(task.Id, task.Type, token.Variables, cancellationToken);
			return;
		}

		await (task.Type.ToLowerInvariant() switch
		{
			"scripttask" => ExecuteDistributedScriptTaskAsync(task, token, model, trace, cancellationToken),
			"servicetask" => ExecuteDistributedServiceTaskAsync(task, token, model, trace, cancellationToken),
			"usertask" => DispatchDistributedUserTaskAsync(task, token, trace, cancellationToken),
			"businessruletask" => ExecuteDistributedDecisionTaskAsync(task, token, model, trace, cancellationToken),
			_ => ContinueToNextNodeAsync(task.Id, token, model, trace, cancellationToken)
		});
	}

	private async Task ExecuteDistributedScriptTaskAsync(BpmnTask task, ExecutionToken token, BpmnModel model,
		List<string> trace, CancellationToken cancellationToken)
	{
		if (model.ProcessVariables != null)
		{
			foreach (var kv in model.ProcessVariables)
			{
				token.Variables[kv.Key] = kv.Value;
			}
		}
		await ScriptTaskExecution.TryHandleScriptTaskAsync(task, token.Variables, cancellationToken,
							allowCSharp: _configuration?.GetValue("Runtime:Scripts:AllowCSharp", false) ?? false,
							scriptsEnabled: _configuration?.GetValue("Runtime:Scripts:Enabled", true) ?? true);
		if (model.ProcessVariables != null)
		{
			foreach (var kv in token.Variables)
			{
				model.ProcessVariables[kv.Key] = kv.Value;
			}
		}
		trace.Add($"ScriptTaskCompleted: {task.Id}");
		await ContinueToNextNodeAsync(task.Id, token, model, trace, cancellationToken);
	}

	private async Task ExecuteDistributedServiceTaskAsync(BpmnTask task, ExecutionToken token, BpmnModel model,
		List<string> trace, CancellationToken cancellationToken)
	{
		var attributes = task.Attributes ?? new Dictionary<string, string>(StringComparer.Ordinal);
		var variables = token.Variables ?? new Dictionary<string, object>(StringComparer.Ordinal);
		if (attributes.TryGetValue("zeebe:taskDefinition", out var taskDef))
		{
			attributes["implementation"] = taskDef;
		}
		if (_serviceRegistry.TryResolve(attributes.GetValueOrDefault("implementation", ""),
								out var handler) && handler is not null)
		{
			trace.Add($"ServiceTask: local handler found for {task.Implementation}");
			await handler.ExecuteAsync(attributes, variables, cancellationToken);
			trace.Add($"ServiceTaskCompleted(local): {task.Id}");
		}
		else
		{
			var targetWorker = token.AssignedWorker ?? (await FindBestWorkerAsync(task.Type))?.Id;
			await _messageDispatcher.DispatchServiceTaskAsync(targetWorker ?? "", attributes.GetValueOrDefault("implementation", ""), attributes, variables, token.ProcessInstanceId, null, cancellationToken);
			trace.Add($"ServiceTaskDispatched: {task.Id} -> {targetWorker ?? "none"}");
		}
		if (model.ProcessVariables == null)
		{
			model = model with { ProcessVariables = new Dictionary<string, object>(variables, StringComparer.Ordinal) };
		}
		else
		{
			foreach (var kv in variables)
			{
				model.ProcessVariables[kv.Key] = kv.Value;
			}
		}
		token = new ExecutionToken
		{
			Id = token.Id,
			ProcessInstanceId = token.ProcessInstanceId,
			CurrentNodeId = token.CurrentNodeId,
			NodeType = token.NodeType,
			Variables = new Dictionary<string, object>(variables, StringComparer.Ordinal),
			CreatedAt = token.CreatedAt,
			AssignedAt = token.AssignedAt,
			RetryCount = token.RetryCount,
			State = token.State
		};
		await ContinueToNextNodeAsync(task.Id, token, model, trace, cancellationToken);
	}

	private async Task DispatchDistributedUserTaskAsync(BpmnTask task, ExecutionToken token,
		List<string> trace, CancellationToken cancellationToken)
	{
		var attributes = task.Attributes ?? new Dictionary<string, string>(StringComparer.Ordinal);
		var variables = token.Variables ?? new Dictionary<string, object>(StringComparer.Ordinal);
		if (attributes.TryGetValue("camunda:assignee", out var assignee) ||
							attributes.TryGetValue("flowable:assignee", out assignee) ||
							attributes.TryGetValue("cib:assignee", out assignee))
		{
			trace.Add($"UserTask: {task.Id} assigned to {assignee}");
			await _messageDispatcher.DispatchUserTaskAsync(assignee, task.Id, variables, cancellationToken);
		}
		else
		{
			trace.Add($"UserTask: {task.Id} no assignee defined");
		}
	}

	private async Task ExecuteDistributedDecisionTaskAsync(BpmnTask task, ExecutionToken token, BpmnModel model,
		List<string> trace, CancellationToken cancellationToken)
	{
		var attributes = task.Attributes ?? new Dictionary<string, string>(StringComparer.Ordinal);
		if (attributes.TryGetValue("vertex:decision.decisionRef", out var decisionRef)
			|| attributes.TryGetValue("camunda:decisionRef", out decisionRef)
			|| attributes.TryGetValue("flowable:decisionRef", out decisionRef))
		{
			attributes.TryGetValue("camunda:resultVariable", out var resultVariable);
			resultVariable ??= attributes.TryGetValue("flowable:resultVariable", out var flowableResult)
				? flowableResult
				: "decisionResult";
			var decisionInputs = BuildDecisionInputs(attributes, token.Variables);
			trace.Add($"BusinessRuleTask: {task.Id} evaluating decision {decisionRef}");
			var decisionResult = await EvaluateTaskDecisionAsync(task, token, decisionRef, decisionInputs, trace, cancellationToken);
			token.Variables[resultVariable] = decisionResult;
			if (model.ProcessVariables == null)
			{
				model = model with { ProcessVariables = new Dictionary<string, object>(StringComparer.Ordinal) };
			}
			model.ProcessVariables[resultVariable] = decisionResult;
			ApplyDecisionOutputMappings(attributes, decisionResult, token.Variables, model.ProcessVariables);
			trace.Add($"BusinessRuleTaskCompleted: {task.Id} result stored in {resultVariable}");
		}
		else
		{
			trace.Add($"BusinessRuleTask: {task.Id} no decisionRef defined");
		}
		await ContinueToNextNodeAsync(task.Id, token, model, trace, cancellationToken);
	}

	private async Task<Dictionary<string, object?>> EvaluateTaskDecisionAsync(
		BpmnTask task, ExecutionToken token, string decisionRef, Dictionary<string, object> decisionInputs,
		List<string> trace, CancellationToken cancellationToken)
	{
		try
		{
			var targetWorker = token.AssignedWorker ?? (await FindBestWorkerAsync(task.Type))?.Id;
			if (targetWorker != null && (await _store.GetWorkerAsync(targetWorker))?.SupportsDmn == true)
			{
				trace.Add($"BusinessRuleTask: dispatching to DMN-capable worker {targetWorker}");
				return await _messageDispatcher.DispatchDmnTaskAsync(targetWorker, decisionRef, decisionInputs, cancellationToken);
			}
			var result = await EvaluateLocalTaskDecisionAsync(decisionRef, decisionInputs, cancellationToken);
#pragma warning disable CS8619 // Widen value nullability at the remote/local DMN boundary without copying the decision result.
			return result;
#pragma warning restore CS8619
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

	private async Task<Dictionary<string, object>> EvaluateLocalTaskDecisionAsync(
		string decisionRef, Dictionary<string, object> decisionInputs, CancellationToken cancellationToken)
	{
		var dmnXml = await _store.GetDmnModelAsync(decisionRef, cancellationToken)
			?? throw new DistributedTokenException($"DMN model {decisionRef} not found");
		var decision = await _dmnParser.ParseAsync(dmnXml, cancellationToken);
		if (!string.IsNullOrWhiteSpace(decision.SourceXml))
		{
			decision = decision with { EvaluationTargetId = decisionRef };
		}
		return await _dmnEngine.EvaluateDecisionAsync(decision, decisionInputs, cancellationToken);
	}


	private static Dictionary<string, object> BuildDecisionInputs(
		IReadOnlyDictionary<string, string> attributes,
		IReadOnlyDictionary<string, object> variables)
	{
		const string prefix = "vertex:ioMapping.input.";
		var mappings = attributes.Where(attribute => attribute.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToList();
		if (mappings.Count == 0)
		{
			return new Dictionary<string, object>(variables, StringComparer.Ordinal);
		}

		return mappings.ToDictionary(
			attribute => attribute.Key[prefix.Length..],
			attribute => ResolveDecisionInput(attribute.Value, variables),
			StringComparer.Ordinal);
	}

	private static object ResolveDecisionInput(string expression, IReadOnlyDictionary<string, object> variables)
	{
		var key = expression.Trim();
		if (key.StartsWith("${", StringComparison.Ordinal) && key.EndsWith('}'))
		{
			key = key[2..^1].Trim();
		}

		if (variables.TryGetValue(key, out var value))
		{
			return value;
		}

		if (bool.TryParse(key, out var boolean))
		{
			return boolean;
		}

		if (long.TryParse(key, out var integer))
		{
			return integer;
		}

		if (decimal.TryParse(key, System.Globalization.CultureInfo.InvariantCulture, out var number))
		{
			return number;
		}

		return key;
	}

	private static void ApplyDecisionOutputMappings<TValue>(
		IReadOnlyDictionary<string, string> attributes,
		Dictionary<string, TValue> decisionResult,
		Dictionary<string, object> tokenVariables,
		Dictionary<string, object> processVariables) where TValue : class?
	{
		const string prefix = "vertex:ioMapping.output.";
		foreach (var mapping in attributes.Where(attribute => attribute.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
		{
			var outputName = mapping.Key[prefix.Length..];
			if (!decisionResult.TryGetValue(outputName, out var value) || string.IsNullOrWhiteSpace(mapping.Value))
			{
				continue;
			}
			// Legacy process-variable dictionaries are annotated non-nullable, but BPMN/FEEL
			// null is a valid runtime value and must not be dropped or replaced by a sentinel.
			tokenVariables[mapping.Value] = value!;
			processVariables[mapping.Value] = value!;
		}
	}

	private async Task ProcessHumanTaskAsync(PlanItem planItem, CaseToken token, CaseModel model,
		List<string> trace, CancellationToken cancellationToken)
	{
		var attributes = planItem.Attributes ?? new Dictionary<string, string>(StringComparer.Ordinal);
		if (attributes.TryGetValue("camunda:assignee", out var assignee) ||
			attributes.TryGetValue("flowable:assignee", out assignee))
		{
			trace.Add($"HumanTask: {planItem.Id} assigned to {assignee}");
			await _messageDispatcher.DispatchUserTaskAsync(assignee, planItem.Id, token.CaseFile,
				cancellationToken);
		}
		else
		{
			trace.Add($"HumanTask: {planItem.Id} no assignee defined");
		}

		await CompletePlanItemAsync(planItem, token, model, trace, cancellationToken);
	}

	private async Task ProcessProcessTaskAsync(PlanItem planItem, CaseToken token, CaseModel model,
		List<string> trace, CancellationToken cancellationToken)
	{
		var attributes = planItem.Attributes ?? new Dictionary<string, string>(StringComparer.Ordinal);
		if (attributes.TryGetValue("camunda:processRef", out var processRef) ||
			attributes.TryGetValue("flowable:processRef", out processRef))
		{
			trace.Add($"ProcessTask: {planItem.Id} starting process {processRef}");
			var bpmnModel = new BpmnModel(processRef, processRef); // Placeholder, lade echtes Modell
			var processTrace = await ExecuteAsync(bpmnModel, cancellationToken);
			trace.AddRange(processTrace);
		}

		await CompletePlanItemAsync(planItem, token, model, trace, cancellationToken);
	}

	private async Task ProcessCaseTaskAsync(PlanItem planItem, CaseToken token, CaseModel model, List<string> trace,
		CancellationToken cancellationToken)
	{
		var attributes = planItem.Attributes ?? new Dictionary<string, string>(StringComparer.Ordinal);
		if (attributes.TryGetValue("vertex:case.caseRef", out var caseRef) ||
			attributes.TryGetValue("camunda:caseRef", out caseRef) ||
			attributes.TryGetValue("flowable:caseRef", out caseRef))
		{
			trace.Add($"CaseTask: {planItem.Id} starting case {caseRef}");
			var cmmnXml = await _store.GetCmmnModelAsync(caseRef)
						  ?? throw new DistributedTokenException($"CMMN model {caseRef} not found");
			var caseModel = await _cmmnParser.ParseAsync(cmmnXml, cancellationToken);
			var caseTrace = await ExecuteCaseAsync(caseModel, cancellationToken);
			trace.AddRange(caseTrace);
		}

		await CompletePlanItemAsync(planItem, token, model, trace, cancellationToken);
	}

	private async Task CompletePlanItemAsync(PlanItem planItem, CaseToken token, CaseModel model,
		List<string> trace, CancellationToken cancellationToken)
	{
		trace.Add($"PlanItemCompleted: {planItem.Id}");
		foreach (var dependentItem in model.PlanItems.Where(pi =>
					 pi.EntrySentryRefs?.Any(sr =>
						 model.Sentries.Any(s => s.Id == sr && s.OnPartRef == planItem.Id)) == true))
		{
			var newToken = new CaseToken(
				Guid.NewGuid(),
				token.CaseInstanceId,
				dependentItem.Id,
				dependentItem.Type,
				new Dictionary<string, object>(token.CaseFile, StringComparer.Ordinal),
				DateTime.UtcNow
			);
			await DistributeCaseTokenAsync(newToken, cancellationToken);
			trace.Add($"CaseTokenDistributed: {newToken.Id} -> {dependentItem.Id}");
		}
	}

	private async Task ProcessSubprocessAsync(BpmnSubprocess subprocess, ExecutionToken token, BpmnModel model,
		List<string> trace, CancellationToken cancellationToken)
	{
		trace.Add($"Subprocess: {subprocess.Id}");
		if (subprocess.IsMultiInstance)
		{
			var cardinality = subprocess.LoopCardinality;
			for (var i = 0; i < cardinality; i++)
			{
				var instanceToken = new ExecutionToken(
					Guid.NewGuid(),
					token.ProcessInstanceId,
					subprocess.Id,
					"subprocess",
					new Dictionary<string, object>(token.Variables, StringComparer.Ordinal),
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

	private async Task ProcessGatewayAsync(BpmnGateway gateway, ExecutionToken token, BpmnModel model,
		List<string> trace, CancellationToken cancellationToken)
	{
		var outgoingFlows = _executionComponent.GetOutgoingFlows(model, gateway.Id);
		switch (gateway.Type)
		{
			case "parallelGateway":
				await DistributeGatewayBranchesAsync(outgoingFlows, token, model, trace, "ParallelBranch", cancellationToken);

				break;

			case "exclusiveGateway":

				var decision = _executionComponent.SelectExclusiveFlow(
					outgoingFlows,
					token.Variables,
					static (flow, variables) => BpmnConditionEvaluator.Evaluate(flow, variables));

				if (decision.Kind == GatewayDecisionKind.NoOutgoingFlow)
				{
					throw new DistributedTokenException(
						$"No valid outgoing SequenceFlow for exclusiveGateway '{gateway.Id}'.");
				}

				var selectedFlow = decision.Flow!;

				await DistributeExclusiveGatewaySelectionAsync(token, model, selectedFlow, decision.Kind, trace, cancellationToken);
				break;

			case "inclusiveGateway":
				var matchingFlows = _executionComponent.SelectInclusiveFlows(
					outgoingFlows,
					token.Variables,
					static (flow, variables) => BpmnConditionEvaluator.Evaluate(flow, variables));

				if (matchingFlows.Count == 0)
				{
					throw new DistributedTokenException(
						$"No valid outgoing SequenceFlow for inclusiveGateway '{gateway.Id}'.");
				}

				await DistributeGatewayBranchesAsync(matchingFlows, token, model, trace, "InclusiveBranch", cancellationToken);

				break;

			case "eventBasedGateway":
				await DistributeGatewayBranchesAsync(outgoingFlows, token, model, trace, "EventBasedBranch", cancellationToken);

				break;

			default:
				throw new DistributedTokenException($"Unsupported gateway type: {gateway.Type}");
		}
	}

	private async Task DistributeExclusiveGatewaySelectionAsync(
		ExecutionToken token,
		BpmnModel model,
		BpmnSequenceFlow selectedFlow,
		GatewayDecisionKind decisionKind,
		List<string> trace,
		CancellationToken cancellationToken)
	{
		var executionToken = new ExecutionToken
		{
			Id = token.Id,
			ProcessInstanceId = token.ProcessInstanceId,
			CurrentNodeId = selectedFlow.TargetRef,
			NodeType = GetNodeType(model, selectedFlow.TargetRef),
			Variables = new Dictionary<string, object>(token.Variables, StringComparer.Ordinal),
			CreatedAt = token.CreatedAt,
			AssignedAt = DateTime.UtcNow,
			RetryCount = token.RetryCount,
			State = token.State
		};

		await DistributeTokenAsync(executionToken, cancellationToken);

		trace.Add(
			$"{decisionKind}: {selectedFlow.TargetRef}");
	}

	private async Task DistributeGatewayBranchesAsync(
		IReadOnlyList<BpmnSequenceFlow> flows,
		ExecutionToken token,
		BpmnModel model,
		List<string> trace,
		string traceLabel,
		CancellationToken cancellationToken)
	{
		foreach (var targetNodeId in flows.Select(flow => flow.TargetRef))
		{
			var newToken = new ExecutionToken(
				Guid.NewGuid(),
				token.ProcessInstanceId,
				targetNodeId,
				GetNodeType(model, targetNodeId),
				new Dictionary<string, object>(token.Variables, StringComparer.Ordinal),
				DateTime.UtcNow);
			await DistributeTokenAsync(newToken, cancellationToken);
			trace.Add($"{traceLabel}: {targetNodeId}");
		}
	}

	private async Task ContinueToNextNodeAsync(string currentNodeId, ExecutionToken token, BpmnModel model,
		List<string> trace, CancellationToken cancellationToken)
	{
		var outgoingFlows = _executionComponent.GetOutgoingFlows(model, currentNodeId);
		foreach (var flow in outgoingFlows)
		{
			trace.Add($"SequenceFlow: {flow.Id}");
			var newToken = new ExecutionToken
			{
				Id = token.Id,
				ProcessInstanceId = token.ProcessInstanceId,
				CurrentNodeId = flow.TargetRef,
				NodeType = GetNodeType(model, flow.TargetRef),
				Variables = new Dictionary<string, object>(token.Variables, StringComparer.Ordinal),
				CreatedAt = token.CreatedAt,
				AssignedAt = DateTime.UtcNow,
				RetryCount = token.RetryCount,
				State = token.State
			};
			await DistributeTokenAsync(newToken, cancellationToken);
		}
	}


	private async Task<bool> EvaluateSentriesAsync(List<string>? sentryRefs, CaseModel model,
		Dictionary<string, object> caseFile, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		if (sentryRefs == null || sentryRefs.Count == 0)
		{
			return true;
		}

		foreach (var sentryId in sentryRefs)
		{
			cancellationToken.ThrowIfCancellationRequested();
			var sentry = model.Sentries.FirstOrDefault(s => s.Id == sentryId);
			if (sentry == null)
			{
				continue;
			}

			var allConditionsMet = sentry.Conditions.All(c => c.LogicalOperator == "OR");
			foreach (var condition in sentry.Conditions)
			{
				var conditionMet = EvaluateCondition(condition, caseFile);
				if (!conditionMet)
				{
					return false;
				}

				conditionMet = await EvaluateSentryExpressionAndEventAsync(sentry, condition, model, caseFile, conditionMet, cancellationToken);
				if (condition.LogicalOperator == "AND" && !conditionMet)
				{
					return false;
				}

				if (condition.LogicalOperator == "OR" && conditionMet)
				{
					allConditionsMet = true;
				}
			}

			if (sentry.Conditions.Any(c => c.LogicalOperator == "OR") && !allConditionsMet)
			{
				return false;
			}
		}

		return true;
	}

	private async Task<bool> EvaluateSentryExpressionAndEventAsync(
		Sentry sentry,
		SentryCondition condition,
		CaseModel model,
		Dictionary<string, object> caseFile,
		bool conditionMet,
		CancellationToken cancellationToken)
	{
		if (!string.IsNullOrEmpty(condition.VariableRef) &&
			caseFile.TryGetValue(condition.VariableRef, out var value))
		{
			using var engine = new Jint.Engine();
			engine.SetValue("input", value);
			conditionMet = (await engine.EvaluateAsync(condition.Expression, cancellationToken: cancellationToken)).AsBoolean();
		}

		if (!string.IsNullOrEmpty(sentry.OnPartRef) && !string.IsNullOrEmpty(condition.OnPartEvent))
		{
			var onPartItem = model.PlanItems.FirstOrDefault(pi => pi.Id == sentry.OnPartRef);
			if (onPartItem != null)
			{
				// Prüfe Zustand des OnPart-Items (z.B. complete, occur)
				var tokens = await _store.GetPendingCaseTokensAsync();
				conditionMet &= tokens.Any(t =>
					t.CurrentPlanItemId == sentry.OnPartRef && condition.OnPartEvent == "complete");
			}
		}

		return conditionMet;
	}

	private static bool EvaluateCondition(SentryCondition condition, Dictionary<string, object> caseFile)
	{
		// Beispiel: Vereinfachte Logik für Bedingungsprüfung
		if (caseFile.ContainsKey(condition.VariableRef))
		{
			return condition.Expression switch
			{
				"complete" => true, // Beispiel: Immer wahr, wenn Variable existiert
				_ => false // Erweitern für komplexere Bedingungen
			};
		}

		return false;
	}

	private static object? FindNode(BpmnModel model, string nodeId)
	{
		return (model.Events ?? []).FirstOrDefault(e => e.Id == nodeId) as object
			   ?? (model.Tasks ?? []).FirstOrDefault(t => t.Id == nodeId) as object
			   ?? (model.Gateways ?? []).FirstOrDefault(g => g.Id == nodeId) as object
			   ?? (model.Subprocesses ?? []).FirstOrDefault(s => s.Id == nodeId) as object;
	}

	private static string GetNodeType(BpmnModel model, string nodeId)
	{
		if ((model.Events ?? []).Any(e => e.Id == nodeId))
		{
			return (model.Events ?? []).First(e => e.Id == nodeId).Type;
		}

		if ((model.Tasks ?? []).Any(t => t.Id == nodeId))
		{
			return (model.Tasks ?? []).First(t => t.Id == nodeId).Type;
		}

		if ((model.Gateways ?? []).Any(g => g.Id == nodeId))
		{
			return (model.Gateways ?? []).First(g => g.Id == nodeId).Type;
		}

		if ((model.Subprocesses ?? []).Any(s => s.Id == nodeId))
		{
			return "subprocess";
		}

		return "unknown";
	}

	private async Task ProcessHeartbeatsAsync()
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

	private static TimeSpan ParseDuration(string duration)
	{
		// Vereinfachte ISO 8601 Parser (für Produktion: System.Xml.XmlConvert verwenden)
		if (duration.StartsWith("PT", StringComparison.Ordinal))
		{
			var timePart = duration[2..];
			if (timePart.EndsWith('S'))
			{
				return TimeSpan.FromSeconds(double.Parse(timePart.TrimEnd('S')));
			}

			if (timePart.EndsWith('M'))
			{
				return TimeSpan.FromMinutes(double.Parse(timePart.TrimEnd('M')));
			}
		}

		return TimeSpan.FromMinutes(1);
	}
	private static bool MatchesCorrelation(
		string? correlationKey,
		ExecutionToken token,
		Message message)
	{
		if (string.IsNullOrWhiteSpace(correlationKey))
		{
			return true;
		}

		if (!token.Variables.TryGetValue(
				correlationKey,
				out var expectedValue))
		{
			return false;
		}

		if (!message.Variables.TryGetValue(
				correlationKey,
				out var receivedValue))
		{
			return false;
		}

		return Equals(
			expectedValue,
			receivedValue);
	}

	public void Dispose()
	{
		Dispose(true);
		GC.SuppressFinalize(this);
	}

	protected virtual void Dispose(bool disposing)
	{
		if (disposing)
		{
			_heartbeatTimer?.Dispose();
		}
	}
}
