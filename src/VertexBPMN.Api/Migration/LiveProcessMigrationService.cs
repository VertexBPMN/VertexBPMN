using System.Collections.Concurrent;
using System.Text.Json;
using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Domain.Interfaces.Repositories;
using VertexBPMN.Infrastructure.Persistence;

namespace VertexBPMN.Api.Migration;
internal sealed class LiveProcessMigrationService : ILiveProcessMigrationService, IDisposable
{
	private readonly ILogger<LiveProcessMigrationService> _logger;
	private readonly IServiceProvider _serviceProvider;
	private readonly ConcurrentDictionary<Guid, MigrationExecution> _activeMigrations = new();
	private readonly ConcurrentDictionary<Guid, LiveMigrationSnapshot> _snapshots = new();
	private readonly SemaphoreSlim _migrationSemaphore = new(5); // Max 5 concurrent migrations
	public void Dispose() => _migrationSemaphore.Dispose();
	public LiveProcessMigrationService(ILogger<LiveProcessMigrationService> logger, IServiceProvider serviceProvider)
	{
		_logger = logger;
		_serviceProvider = serviceProvider;
	}

	public async Task<MigrationPlan> CreateMigrationPlanAsync(string fromProcessKey, string toProcessKey, MigrationOptions options, string? tenantId = null)
	{
		using var scope = _serviceProvider.CreateScope();
		var fromProcess = await GetProcessDefinitionAsync(fromProcessKey, tenantId, scope);
		var toProcess = await GetProcessDefinitionAsync(toProcessKey, tenantId, scope);
		return await CreateMigrationPlanCoreAsync(fromProcess, toProcess, options, scope);
	}

	public async Task<MigrationPlan> CreateMigrationPlanByDefinitionIdAsync(Guid fromProcessDefinitionId, Guid toProcessDefinitionId, MigrationOptions options, string? tenantId = null)
	{
		using var scope = _serviceProvider.CreateScope();
		var fromProcess = await GetProcessDefinitionByIdAsync(fromProcessDefinitionId, tenantId, scope);
		var toProcess = await GetProcessDefinitionByIdAsync(toProcessDefinitionId, tenantId, scope);
		return await CreateMigrationPlanCoreAsync(fromProcess, toProcess, options, scope);
	}

	private async Task<MigrationPlan> CreateMigrationPlanCoreAsync(ProcessDefinitionInfo fromProcess, ProcessDefinitionInfo toProcess, MigrationOptions options, IServiceScope scope)
	{
		if (!string.Equals(fromProcess.TenantId, toProcess.TenantId, StringComparison.Ordinal))
		{
			throw new InvalidOperationException("Source and target process definitions must belong to the same tenant.");
		}

		var compatibilityIssues = ValidateCompatibility(fromProcess, toProcess);
		var activeInstances = await GetActiveProcessInstancesAsync(fromProcess.Id, fromProcess.Key, scope);
		var mappingStrategy = await CreateActivityMappingAsync(fromProcess, toProcess);
		var complexity = CalculateMigrationComplexity(activeInstances.Count, compatibilityIssues.Count, mappingStrategy);
		var migrationPlan = new MigrationPlan
		{
			Id = Guid.NewGuid(),
			FromProcessDefinitionId = fromProcess.Id,
			ToProcessDefinitionId = toProcess.Id,
			TenantId = fromProcess.TenantId,
			FromProcessKey = fromProcess.Key,
			ToProcessKey = toProcess.Key,
			Options = options,
			CreatedAt = DateTime.UtcNow,
			EstimatedDuration = CalculateEstimatedDuration(activeInstances.Count, complexity),
			Complexity = complexity,
			CompatibilityIssues = compatibilityIssues,
			ActivityMappings = mappingStrategy,
			AffectedInstances = activeInstances.Count,
			MigrationSteps = GenerateMigrationSteps(fromProcess.Key, toProcess.Key),
			RiskAssessment = AssessMigrationRisk(compatibilityIssues, activeInstances.Count),
			RollbackPlan = CreateRollbackPlan()
		};
		await StoreMigrationPlanAsync(migrationPlan, scope);
		if (_logger.IsEnabled(Microsoft.Extensions.Logging.LogLevel.Information))
		{
			_logger.LogInformation("Migration plan {PlanId} binds definition {SourceDefinitionId} to {TargetDefinitionId} for tenant {TenantId} and affects {InstanceCount} instances", migrationPlan.Id, fromProcess.Id, toProcess.Id, fromProcess.TenantId, activeInstances.Count);
		}
		return migrationPlan;
	}

	public async Task<MigrationExecution> ExecuteMigrationAsync(Guid migrationPlanId, bool dryRun = false, string? tenantId = null)
	{
		if (!await _migrationSemaphore.WaitAsync(TimeSpan.FromMinutes(5)))
		{
			throw new InvalidOperationException("Migration service is currently at capacity. Please try again later.");
		}

		try
		{
			if (_logger.IsEnabled(Microsoft.Extensions.Logging.LogLevel.Information))
			{
				_logger.LogInformation("Executing migration plan {PlanId} (DryRun: {DryRun})", migrationPlanId, dryRun);
			}
			using var scope = _serviceProvider.CreateScope();
			var migrationPlan = await GetMigrationPlanAsync(migrationPlanId, scope);
			EnsureTenantAccess(migrationPlan.TenantId, tenantId);
			var execution = new MigrationExecution
			{
				Id = Guid.NewGuid(),
				MigrationPlanId = migrationPlanId,
				TenantId = migrationPlan.TenantId,
				StartedAt = DateTime.UtcNow,
				Status = MigrationStatus.InProgress,
				IsDryRun = dryRun,
				Progress = 0,
				Steps = new List<MigrationStepResult>(),
				Snapshots = new List<Guid>()
			};
			_activeMigrations[execution.Id] = execution;
			await StoreMigrationExecutionAsync(execution, scope);
			try
			{
				await ExecuteTransactionalMigrationAsync(execution, migrationPlan, scope);
				execution.Status = MigrationStatus.Completed;
				execution.CompletedAt = DateTime.UtcNow;
				execution.Progress = 100;
				await StoreMigrationExecutionAsync(execution, scope);
				if (_logger.IsEnabled(Microsoft.Extensions.Logging.LogLevel.Information))
				{
					_logger.LogInformation("Migration {ExecutionId} completed successfully", execution.Id);
				}
			}
			catch (Exception ex)
			{
				execution.Status = MigrationStatus.Failed;
				execution.Error = ex.Message;
				execution.CompletedAt = DateTime.UtcNow;
				await StoreMigrationExecutionAsync(execution, scope);
				_logger.LogError(ex, "Migration {ExecutionId} failed", execution.Id);
			// Runtime mutations are enclosed in one relational transaction. A failed
			// execution is already rolled back before its failure record is stored.
			}

			return execution;
		}
		finally
		{
			_migrationSemaphore.Release();
		}
	}

	public async Task<MigrationStatus> GetMigrationStatusAsync(Guid migrationId, string? tenantId = null)
	{
		if (_activeMigrations.TryGetValue(migrationId, out var execution))
		{
			EnsureTenantAccess(execution.TenantId, tenantId);
			return execution.Status;
		}

		// Check completed migrations in storage
		using var scope = _serviceProvider.CreateScope();
		var storedExecution = await GetStoredMigrationExecutionAsync(migrationId, scope);
		if (storedExecution is not null)
		{
			EnsureTenantAccess(storedExecution.TenantId, tenantId);
		}

		return storedExecution?.Status ?? MigrationStatus.NotFound;
	}

	public async Task<bool> RollbackMigrationAsync(Guid migrationId, string? tenantId = null)
	{
		try
		{
			if (_logger.IsEnabled(Microsoft.Extensions.Logging.LogLevel.Information))
			{
				_logger.LogInformation("Starting rollback for migration {MigrationId}", migrationId);
			}
			if (!_activeMigrations.TryGetValue(migrationId, out var execution))
			{
				using var scope = _serviceProvider.CreateScope();
				execution = await GetStoredMigrationExecutionAsync(migrationId, scope);
				if (execution == null)
				{
					_logger.LogWarning("Migration {MigrationId} not found for rollback", migrationId);
					return false;
				}
			}

			EnsureTenantAccess(execution.TenantId, tenantId);
			execution.Status = MigrationStatus.RollingBack;
			// Restore from snapshots in reverse order
			foreach (var snapshotId in execution.Snapshots.AsEnumerable().Reverse())
			{
				LiveMigrationSnapshot? snapshot;
				if (_snapshots.TryGetValue(snapshotId, out var cachedSnapshot))
				{
					snapshot = cachedSnapshot;
				}
				else
				{
					using var snapshotScope = _serviceProvider.CreateScope();
					snapshot = await GetStoredSnapshotAsync(snapshotId, snapshotScope);
				}

				if (snapshot is not null)
				{
					await RestoreFromSnapshotAsync(snapshot.ProcessInstanceId, snapshotId, tenantId);
				}
			}

			execution.Status = MigrationStatus.RolledBack;
			execution.CompletedAt = DateTime.UtcNow;
			using var storeScope = _serviceProvider.CreateScope();
			await StoreMigrationExecutionAsync(execution, storeScope);
			if (_logger.IsEnabled(Microsoft.Extensions.Logging.LogLevel.Information))
			{
				_logger.LogInformation("Rollback completed for migration {MigrationId}", migrationId);
			}
			return true;
		}
		catch (UnauthorizedAccessException)
		{
			throw;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error during rollback of migration {MigrationId}", migrationId);
			return false;
		}
	}

	public async Task<List<MigrationCompatibilityIssue>> ValidateCompatibilityAsync(string fromProcessKey, string toProcessKey, string? tenantId = null)
	{
		using var scope = _serviceProvider.CreateScope();
		var fromProcess = await GetProcessDefinitionAsync(fromProcessKey, tenantId, scope);
		var toProcess = await GetProcessDefinitionAsync(toProcessKey, tenantId, scope);
		return ValidateCompatibility(fromProcess, toProcess);
	}

	private static List<MigrationCompatibilityIssue> ValidateCompatibility(ProcessDefinitionInfo fromProcess, ProcessDefinitionInfo toProcess)
	{
		var issues = new List<MigrationCompatibilityIssue>();
		// Check activity compatibility
		var fromActivities = ExtractActivities(fromProcess);
		var toActivities = ExtractActivities(toProcess);
		foreach (var fromActivity in fromActivities)
		{
			var mappedActivity = toActivities.FirstOrDefault(a => a.Name == fromActivity.Name);
			if (mappedActivity == null)
			{
				issues.Add(new MigrationCompatibilityIssue { Type = "ActivityNotFound", Severity = "High", Description = $"Activity '{fromActivity.Name}' not found in target process", Recommendation = "Add activity mapping or update target process definition", AffectedElement = fromActivity.Id });
			}
			else if (fromActivity.Type != mappedActivity.Type)
			{
				issues.Add(new MigrationCompatibilityIssue { Type = "ActivityTypeMismatch", Severity = "Medium", Description = $"Activity '{fromActivity.Name}' type changed from {fromActivity.Type} to {mappedActivity.Type}", Recommendation = "Review activity behavior compatibility", AffectedElement = fromActivity.Id });
			}
		}

		// Check variable compatibility
		var fromVariables = ExtractVariables(fromProcess);
		var toVariables = ExtractVariables(toProcess);
		foreach (var fromVar in fromVariables)
		{
			var mappedVar = toVariables.FirstOrDefault(v => v.Name == fromVar.Name);
			if (mappedVar == null)
			{
				issues.Add(new MigrationCompatibilityIssue { Type = "VariableNotFound", Severity = "Medium", Description = $"Variable '{fromVar.Name}' not found in target process", Recommendation = "Add variable or create mapping rule", AffectedElement = fromVar.Name });
			}
			else if (fromVar.Type != mappedVar.Type)
			{
				issues.Add(new MigrationCompatibilityIssue { Type = "VariableTypeMismatch", Severity = "High", Description = $"Variable '{fromVar.Name}' type changed from {fromVar.Type} to {mappedVar.Type}", Recommendation = "Create type conversion rule", AffectedElement = fromVar.Name });
			}
		}

		return issues;
	}

	public async Task<LiveMigrationSnapshot> CreateSnapshotAsync(Guid processInstanceId, string? tenantId = null)
	{
		using var scope = _serviceProvider.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<BpmnDbContext>();
		var instance = await db.ProcessInstances.SingleOrDefaultAsync(item => item.Id == processInstanceId) ?? throw new InvalidOperationException($"Process instance '{processInstanceId}' was not found.");
		EnsureTenantAccess(instance.TenantId, tenantId);
		var snapshot = await CreateSnapshotFromDbAsync(db, instance);
		_snapshots[snapshot.Id] = snapshot;
		await db.SaveChangesAsync();
		if (_logger.IsEnabled(Microsoft.Extensions.Logging.LogLevel.Debug))
		{
			_logger.LogDebug("Created snapshot {SnapshotId} for process instance {ProcessInstanceId}", snapshot.Id, processInstanceId);
		}

		return snapshot;
	}

	private static async Task<LiveMigrationSnapshot> CreateSnapshotFromDbAsync(BpmnDbContext db, ProcessInstance instance)
	{
		var tokens = await db.ExecutionTokens.AsNoTracking().Where(item => item.ProcessInstanceId == instance.Id).ToArrayAsync();
		var tasks = await db.Tasks.AsNoTracking().Where(item => item.ProcessInstanceId == instance.Id).ToArrayAsync();
		var jobs = await db.Jobs.AsNoTracking().Where(item => item.ProcessInstanceId == instance.Id).ToArrayAsync();
		var subscriptions = await db.EventSubscriptions.AsNoTracking().Where(item => item.ProcessInstanceId == instance.Id).ToArrayAsync();
		var incidents = await db.Incidents.AsNoTracking().Where(item => item.ProcessInstanceId == instance.Id).ToArrayAsync();
		var multiInstances = await db.MultiInstanceExecutions.AsNoTracking().Where(item => item.ProcessInstanceId == instance.Id).ToArrayAsync();
		var variables = await db.Variables.AsNoTracking().Where(item => item.ProcessInstanceId == instance.Id).ToArrayAsync();
		var snapshot = new LiveMigrationSnapshot
		{
			Id = Guid.NewGuid(),
			ProcessInstanceId = instance.Id,
			TenantId = instance.TenantId,
			CreatedAt = DateTime.UtcNow,
			ProcessState = JsonSerializer.Serialize(new ProcessInstanceState { Id = instance.Id, Status = instance.Status.ToString(), State = instance.State, ProcessDefinitionId = instance.ProcessDefinitionId, ProcessId = instance.ProcessId, ActiveTokens = [..instance.ActiveTokens], ActiveTasks = [..instance.ActiveTasks] }),
			TokenStates = tokens.ToDictionary(token => token.Id.ToString(), token => JsonSerializer.Serialize(new TokenState { Id = token.Id, ProcessInstanceId = token.ProcessInstanceId, ActivityId = token.CurrentNodeId, NodeType = token.NodeType, State = token.State, Variables = token.Variables, CreatedAt = token.CreatedAt, AssignedWorker = token.AssignedWorker, AssignedAt = token.AssignedAt, RetryCount = token.RetryCount }), StringComparer.Ordinal),
			Variables = variables.ToDictionary(variable => variable.Name, variable => JsonSerializer.Serialize(variable.Value), StringComparer.Ordinal),
			ActivityStates = tokens.GroupBy(token => token.CurrentNodeId, StringComparer.Ordinal).ToDictionary(group => group.Key, group => JsonSerializer.Serialize(new ActivityState { Id = group.Key, Status = string.Join(",", group.Select(token => token.State).Distinct(StringComparer.Ordinal)) }), StringComparer.Ordinal),
			TaskActivityIds = tasks.ToDictionary(item => item.Id, item => item.ActivityId),
			JobActivityIds = jobs.ToDictionary(item => item.Id, item => item.ActivityId),
			SubscriptionActivityIds = subscriptions.ToDictionary(item => item.Id, item => item.ActivityId),
			SubscriptionActiveKeys = subscriptions.ToDictionary(item => item.Id, item => item.ActiveKey),
			IncidentActivityIds = incidents.ToDictionary(item => item.Id, item => item.ActivityId),
			MultiInstanceActivityIds = multiInstances.ToDictionary(item => item.Id, item => item.ActivityId)
		};
		db.HistoryEvents.Add(new HistoryEvent { Id = snapshot.Id, ProcessInstanceId = snapshot.ProcessInstanceId, EventType = "LIVE_MIGRATION_SNAPSHOT", Timestamp = snapshot.CreatedAt, Details = "Live process migration snapshot", ElementId = snapshot.ProcessInstanceId.ToString(), Data = JsonSerializer.Serialize(snapshot), TenantId = instance.TenantId });
		return snapshot;
	}

	public async Task<bool> RestoreFromSnapshotAsync(Guid processInstanceId, Guid snapshotId, string? tenantId = null)
	{
		try
		{
			if (!_snapshots.TryGetValue(snapshotId, out var snapshot))
			{
				using var scope = _serviceProvider.CreateScope();
				snapshot = await GetStoredSnapshotAsync(snapshotId, scope);
				if (snapshot == null)
				{
					_logger.LogWarning("Snapshot {SnapshotId} not found", snapshotId);
					return false;
				}
			}

			EnsureTenantAccess(snapshot.TenantId, tenantId);
			using var restoreScope = _serviceProvider.CreateScope();
			var db = restoreScope.ServiceProvider.GetRequiredService<BpmnDbContext>();
			await using var transaction = await db.Database.BeginTransactionAsync();
			var state = JsonSerializer.Deserialize<ProcessInstanceState>(snapshot.ProcessState) ?? throw new InvalidOperationException($"Snapshot '{snapshotId}' has invalid process state.");
			if (state.Id != processInstanceId || snapshot.ProcessInstanceId != processInstanceId)
			{
				throw new InvalidOperationException("Snapshot does not belong to the requested process instance.");
			}

			var instance = await db.ProcessInstances.SingleAsync(item => item.Id == processInstanceId);
			EnsureTenantAccess(instance.TenantId, tenantId);
			instance.ProcessDefinitionId = state.ProcessDefinitionId;
			instance.ProcessId = state.ProcessId;
			instance.State = state.State;
			if (Enum.TryParse<ProcessInstanceStatus>(state.Status, out var status))
			{
				instance.Status = status;
			}

			instance.ActiveTokens = [..state.ActiveTokens];
			instance.ActiveTasks = [..state.ActiveTasks];
			instance.LastModified = DateTime.UtcNow;
			instance.Revision++;
			await RestoreTokenStatesAsync(db, snapshot);

			await RestoreWaitStatesAsync(db, snapshot);

			db.RuntimeOutbox.Add(new RuntimeOutboxMessage { Id = Guid.NewGuid(), ProcessInstanceId = instance.Id, EventType = "ProcessMigrationRolledBack", TenantId = instance.TenantId, OccurredAt = DateTime.UtcNow, Payload = JsonSerializer.Serialize(new { snapshotId }) });
			await db.SaveChangesAsync();
			await transaction.CommitAsync();
			if (_logger.IsEnabled(Microsoft.Extensions.Logging.LogLevel.Information))
			{
				_logger.LogInformation("Successfully restored process instance {ProcessInstanceId} from snapshot {SnapshotId}", processInstanceId, snapshotId);
			}
			return true;
		}
		catch (UnauthorizedAccessException)
		{
			throw;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error restoring process instance {ProcessInstanceId} from snapshot {SnapshotId}", processInstanceId, snapshotId);
			return false;
		}
	}

	private async Task ExecuteTransactionalMigrationAsync(MigrationExecution execution, MigrationPlan plan, IServiceScope scope)
	{
		var db = scope.ServiceProvider.GetRequiredService<BpmnDbContext>();
		var sourceDefinition = await GetMigrationDefinitionAsync(db, plan.FromProcessDefinitionId, plan.FromProcessKey, plan.TenantId, "Source");
		var targetDefinition = await GetMigrationDefinitionAsync(db, plan.ToProcessDefinitionId, plan.ToProcessKey, plan.TenantId, "Target");

		var targetActivities = ExtractActivityElements(targetDefinition.BpmnXml);
		var mappings = plan.ActivityMappings.Where(mapping => !string.IsNullOrWhiteSpace(mapping.ToActivityId)).ToDictionary(mapping => mapping.FromActivityId, mapping => mapping.ToActivityId, StringComparer.Ordinal);
		string Map(string activityId)
		{
			var targetId = mappings.GetValueOrDefault(activityId) ?? (targetActivities.ContainsKey(activityId) ? activityId : null);
			if (string.IsNullOrWhiteSpace(targetId) || !targetActivities.ContainsKey(targetId))
			{
				throw new InvalidOperationException($"Active activity '{activityId}' has no valid mapping in target process '{plan.ToProcessKey}'.");
			}

			return targetId;
		}

		var instances = await db.ProcessInstances.Where(instance => instance.ProcessDefinitionId == sourceDefinition.Id && instance.Status != ProcessInstanceStatus.Completed && instance.Status != ProcessInstanceStatus.Terminated).OrderBy(instance => instance.CreatedAt).ToArrayAsync();
		if (instances.Length != plan.AffectedInstances)
		{
			throw new DbUpdateConcurrencyException($"Migration plan expected {plan.AffectedInstances} active instances but found {instances.Length}.");
		}

		// Validate every current wait-state before entering the write transaction.
		foreach (var instance in instances)
		{
			var activeIds = await ActiveActivityIdsAsync(db, instance.Id);
			foreach (var activityId in activeIds)
			{
				_ = Map(activityId);
			}
		}

		execution.Steps.Add(new MigrationStepResult { StepName = "Validate active runtime mappings", StartedAt = DateTime.UtcNow, CompletedAt = DateTime.UtcNow, Status = "Completed" });
		execution.Progress = execution.IsDryRun ? 100 : 25;
		if (execution.IsDryRun)
		{
			return;
		}

		await using var transaction = await db.Database.BeginTransactionAsync();
		foreach (var instance in instances)
		{
			await MigrateInstanceAsync(db, instance, execution, sourceDefinition, targetDefinition, targetActivities, Map);
		}

		await db.SaveChangesAsync();
		await transaction.CommitAsync();
		execution.Steps.Add(new MigrationStepResult { StepName = "Transactionally migrate runtime state", StartedAt = DateTime.UtcNow, CompletedAt = DateTime.UtcNow, Status = "Completed" });
		execution.Progress = 100;
	}

	private static async Task<ProcessDefinition> GetMigrationDefinitionAsync(BpmnDbContext db, Guid definitionId, string key, string? tenantId, string role)
	{
		var definitions = db.ProcessDefinitions.Where(definition => definition.TenantId == tenantId);
		var definition = definitionId != Guid.Empty
			? await definitions.SingleOrDefaultAsync(value => value.Id == definitionId)
			: await definitions.Where(value => value.Key == key).OrderByDescending(value => value.Version).FirstOrDefaultAsync();
		return definition ?? throw new InvalidOperationException($"{role} process definition '{definitionId}' was not found in tenant '{tenantId}'.");
	}

	private static async Task RestoreTokenStatesAsync(BpmnDbContext db, LiveMigrationSnapshot snapshot)
	{
		foreach (var tokenStateJson in snapshot.TokenStates.Values)
		{
			var tokenState = JsonSerializer.Deserialize<TokenState>(tokenStateJson) ?? throw new InvalidOperationException("Snapshot contains an invalid token.");
			var token = await db.ExecutionTokens.SingleAsync(item => item.Id == tokenState.Id);
			token.CurrentNodeId = tokenState.ActivityId;
			token.NodeType = tokenState.NodeType;
			token.State = tokenState.State;
			token.Variables = tokenState.Variables;
			token.AssignedWorker = tokenState.AssignedWorker;
			token.AssignedAt = tokenState.AssignedAt;
			token.RetryCount = tokenState.RetryCount;
			token.Revision++;
		}
	}

	private static async Task RestoreWaitStatesAsync(BpmnDbContext db, LiveMigrationSnapshot snapshot)
	{
		foreach (var(id, activityId)in snapshot.TaskActivityIds)
		{
			var task = await db.Tasks.SingleAsync(item => item.Id == id);
			task.ActivityId = activityId;
			task.LastModified = DateTime.UtcNow;
			task.Revision++;
		}

		foreach (var(id, activityId)in snapshot.JobActivityIds)
		{
			var job = await db.Jobs.SingleAsync(item => item.Id == id);
			job.ActivityId = activityId;
			job.Revision++;
		}

		foreach (var(id, activityId)in snapshot.SubscriptionActivityIds)
		{
			var subscription = await db.EventSubscriptions.SingleAsync(item => item.Id == id);
			subscription.ActivityId = activityId;
			subscription.ActiveKey = snapshot.SubscriptionActiveKeys.GetValueOrDefault(id);
			subscription.Revision++;
		}

		foreach (var(id, activityId)in snapshot.IncidentActivityIds)
		{
			(await db.Incidents.SingleAsync(item => item.Id == id)).ActivityId = activityId;
		}
		foreach (var(id, activityId)in snapshot.MultiInstanceActivityIds)
		{
			var multiInstance = await db.MultiInstanceExecutions.SingleAsync(item => item.Id == id);
			multiInstance.ActivityId = activityId;
			multiInstance.Revision++;
		}
	}

	private async Task MigrateInstanceAsync(BpmnDbContext db, ProcessInstance instance, MigrationExecution execution, ProcessDefinition sourceDefinition, ProcessDefinition targetDefinition, Dictionary<string, ActivityInfo> targetActivities, Func<string, string> map)
	{
		execution.AffectedProcessInstanceIds.Add(instance.Id);
		var snapshot = await CreateSnapshotFromDbAsync(db, instance);
		execution.Snapshots.Add(snapshot.Id);
		_snapshots[snapshot.Id] = snapshot;
		await MigrateWaitStatesAsync(db, instance.Id, map, targetActivities);

		instance.ProcessDefinitionId = targetDefinition.Id;
		instance.ProcessId = targetDefinition.Key;
		instance.ActiveTokens = instance.ActiveTokens.Select(map).Distinct(StringComparer.Ordinal).ToList();
		instance.ActiveTasks = instance.ActiveTasks.Select(map).Distinct(StringComparer.Ordinal).ToList();
		instance.LastModified = DateTime.UtcNow;
		instance.Revision++;
		db.RuntimeOutbox.Add(new RuntimeOutboxMessage { Id = Guid.NewGuid(), ProcessInstanceId = instance.Id, EventType = "ProcessMigrated", TenantId = instance.TenantId, OccurredAt = DateTime.UtcNow, Payload = JsonSerializer.Serialize(new { execution.Id, fromDefinitionId = sourceDefinition.Id, toDefinitionId = targetDefinition.Id }) });
	}

	private static async Task MigrateWaitStatesAsync(BpmnDbContext db, Guid processInstanceId, Func<string, string> map, Dictionary<string, ActivityInfo> targetActivities)
	{
		var tokens = await db.ExecutionTokens.Where(item => item.ProcessInstanceId == processInstanceId).ToArrayAsync();
		var tasks = await db.Tasks.Where(item => item.ProcessInstanceId == processInstanceId && (item.Status == UserTaskStatus.Pending || item.Status == UserTaskStatus.Delegated)).ToArrayAsync();
		var jobs = await db.Jobs.Where(item => item.ProcessInstanceId == processInstanceId && item.State == "Scheduled").ToArrayAsync();
		var subscriptions = await db.EventSubscriptions.Where(item => item.ProcessInstanceId == processInstanceId && item.State == "Active").ToArrayAsync();
		var incidents = await db.Incidents.Where(item => item.ProcessInstanceId == processInstanceId && item.State != "Resolved").ToArrayAsync();
		var multiInstances = await db.MultiInstanceExecutions.Where(item => item.ProcessInstanceId == processInstanceId && item.State == "Active").ToArrayAsync();
		foreach (var token in tokens)
		{
			token.CurrentNodeId = map(token.CurrentNodeId);
			token.Revision++;
		}

		foreach (var task in tasks)
		{
			task.ActivityId = map(task.ActivityId);
			task.Name = targetActivities[task.ActivityId].Name;
			task.LastModified = DateTime.UtcNow;
			task.Revision++;
		}

		foreach (var job in jobs)
		{
			job.ActivityId = map(job.ActivityId);
			job.Revision++;
		}

		foreach (var subscription in subscriptions)
		{
			subscription.ActivityId = map(subscription.ActivityId);
			subscription.ActiveKey = $"{processInstanceId:N}:{subscription.ActivityId}";
			subscription.Revision++;
		}

		foreach (var incident in incidents.Where(value => !string.IsNullOrWhiteSpace(value.ActivityId)))
		{
			incident.ActivityId = map(incident.ActivityId!);
		}

		foreach (var multiInstance in multiInstances)
		{
			multiInstance.ActivityId = map(multiInstance.ActivityId);
			multiInstance.Revision++;
		}
	}

	private static async Task<HashSet<string>> ActiveActivityIdsAsync(BpmnDbContext db, Guid processInstanceId)
	{
		var activityIds = new HashSet<string>(StringComparer.Ordinal);
		activityIds.UnionWith(await db.ExecutionTokens.Where(item => item.ProcessInstanceId == processInstanceId && item.State != "Completed").Select(item => item.CurrentNodeId).ToArrayAsync());
		activityIds.UnionWith(await db.Tasks.Where(item => item.ProcessInstanceId == processInstanceId && (item.Status == UserTaskStatus.Pending || item.Status == UserTaskStatus.Delegated)).Select(item => item.ActivityId).ToArrayAsync());
		activityIds.UnionWith(await db.Jobs.Where(item => item.ProcessInstanceId == processInstanceId && item.State == "Scheduled").Select(item => item.ActivityId).ToArrayAsync());
		activityIds.UnionWith(await db.EventSubscriptions.Where(item => item.ProcessInstanceId == processInstanceId && item.State == "Active").Select(item => item.ActivityId).ToArrayAsync());
		activityIds.UnionWith(await db.Incidents.Where(item => item.ProcessInstanceId == processInstanceId && item.State != "Resolved" && item.ActivityId != null).Select(item => item.ActivityId!).ToArrayAsync());
		activityIds.UnionWith(await db.MultiInstanceExecutions.Where(item => item.ProcessInstanceId == processInstanceId && item.State == "Active").Select(item => item.ActivityId).ToArrayAsync());
		return activityIds;
	}

	private static Dictionary<string, ActivityInfo> ExtractActivityElements(string bpmnXml)
	{
		var document = XDocument.Parse(bpmnXml, LoadOptions.PreserveWhitespace);
		return document.Descendants().Where(element => element.Attribute("id")is not null && (element.Name.LocalName.EndsWith("Event", StringComparison.Ordinal) || element.Name.LocalName.EndsWith("Task", StringComparison.Ordinal) || element.Name.LocalName.EndsWith("Gateway", StringComparison.Ordinal) || element.Name.LocalName is "subProcess" or "transaction" or "callActivity")).ToDictionary(element => element.Attribute("id")!.Value, element => new ActivityInfo { Id = element.Attribute("id")!.Value, Name = element.Attribute("name")?.Value ?? element.Attribute("id")!.Value, Type = element.Name.LocalName }, StringComparer.Ordinal);
	}

	// Helper methods





	private static async Task<ProcessDefinitionInfo> GetProcessDefinitionAsync(string processKey, string? tenantId, IServiceScope scope)
	{
		var repository = scope.ServiceProvider.GetRequiredService<IProcessDefinitionRepository>();
		var definition = await repository.GetLatestByKeyAsync(processKey, tenantId);
		if (definition is null)
		{
			throw new InvalidOperationException($"Process definition '{processKey}' was not found in tenant '{tenantId}'.");
		}

		return ToProcessDefinitionInfo(definition);
	}

	private static async Task<ProcessDefinitionInfo> GetProcessDefinitionByIdAsync(Guid processDefinitionId, string? tenantId, IServiceScope scope)
	{
		var repository = scope.ServiceProvider.GetRequiredService<IProcessDefinitionRepository>();
		var definition = await repository.GetByIdAsync(processDefinitionId);
		if (definition is null || !string.Equals(definition.TenantId, tenantId, StringComparison.Ordinal))
		{
			throw new InvalidOperationException($"Process definition '{processDefinitionId}' was not found in tenant '{tenantId}'.");
		}

		return ToProcessDefinitionInfo(definition);
	}

	private static ProcessDefinitionInfo ToProcessDefinitionInfo(ProcessDefinition definition) => new()
	{
		Id = definition.Id,
		TenantId = definition.TenantId,
		Key = definition.Key,
		Name = definition.Name,
		Version = definition.Version,
		BpmnXml = definition.BpmnXml
	};
	private static async Task<List<ProcessInstanceInfo>> GetActiveProcessInstancesAsync(Guid processDefinitionId, string processKey, IServiceScope scope)
	{
		var runtimeService = scope.ServiceProvider.GetRequiredService<IRuntimeService>();
		return await CollectActiveInstancesAsync(runtimeService, processDefinitionId, processKey);
	}


	private static async Task<List<ProcessInstanceInfo>> CollectActiveInstancesAsync(IRuntimeService runtimeService, Guid processDefinitionId, string processKey)
	{
		var instances = new List<ProcessInstanceInfo>();
		await foreach (var instance in runtimeService.ListAsync(processDefinitionId, cancellationToken: CancellationToken.None))
		{
			if (instance.Status is ProcessInstanceStatus.Completed or ProcessInstanceStatus.Terminated)
			{
				continue;
			}

			instances.Add(new ProcessInstanceInfo { Id = instance.Id, ProcessDefinitionKey = processKey, Status = instance.Status.ToString() });
		}

		return instances;
	}

	private async Task<List<ActivityMappingRule>> CreateActivityMappingAsync(ProcessDefinitionInfo from, ProcessDefinitionInfo to)
	{
		var targetActivities = ExtractActivities(to);
		return ExtractActivities(from).Select(source =>
		{
			var target = targetActivities.FirstOrDefault(candidate => candidate.Id == source.Id || candidate.Name == source.Name);
			var mappingType = "Unmapped";
			if (target is not null)
			{
				mappingType = target.Type == source.Type ? "Direct" : "Transform";
			}

			return new ActivityMappingRule
			{
				FromActivityId = source.Id,
				ToActivityId = target?.Id ?? string.Empty,
				MappingType = mappingType
			};
		}).ToList();
	}

	private static string CalculateMigrationComplexity(int instanceCount, int issueCount, List<ActivityMappingRule> mappings)
	{
		var score = (instanceCount * 0.1) + (issueCount * 0.5) + (mappings.Count(m => m.MappingType != "Direct") * 0.3);
		return score switch
		{
			< 2 => "Low",
			< 5 => "Medium",
			< 10 => "High",
			_ => "Critical"
		};
	}

	private static TimeSpan CalculateEstimatedDuration(int instanceCount, string complexity)
	{
		var baseMinutes = complexity switch
		{
			"Low" => 5,
			"Medium" => 15,
			"High" => 30,
			"Critical" => 60,
			_ => 10
		};
		return TimeSpan.FromMinutes(baseMinutes + (instanceCount * 0.5));
	}

	private static List<MigrationStep> GenerateMigrationSteps(string fromProcessKey, string toProcessKey)
	{
		return new List<MigrationStep>
		{
			new()
			{
				Name = "Create Snapshots",
				Type = "CreateSnapshots",
				Order = 1,
				Parameters = new Dictionary<string, object>(StringComparer.Ordinal)
				{
					["processKey"] = fromProcessKey
				}
			},
			new()
			{
				Name = "Migrate Instances",
				Type = "MigrateInstances",
				Order = 2,
				Parameters = new Dictionary<string, object>(StringComparer.Ordinal)
				{
					["fromProcessKey"] = fromProcessKey,
					["toProcessKey"] = toProcessKey
				}
			},
			new()
			{
				Name = "Update Definitions",
				Type = "UpdateDefinitionsStep",
				Order = 3,
				Parameters = new Dictionary<string, object>(StringComparer.Ordinal)
				{
					["toProcessKey"] = toProcessKey
				}
			},
			new()
			{
				Name = "Validate Results",
				Type = "ValidateResults",
				Order = 4,
				Parameters = new Dictionary<string, object>(StringComparer.Ordinal)
				{
					["fromProcessKey"] = fromProcessKey,
					["toProcessKey"] = toProcessKey
				}
			}
		};
	}

	private static string AssessMigrationRisk(List<MigrationCompatibilityIssue> issues, int instanceCount)
	{
		var highSeverityIssues = issues.Count(i => i.Severity == "High");
		var riskScore = (highSeverityIssues * 3) + issues.Count + (instanceCount > 10 ? 2 : 0);
		return riskScore switch
		{
			< 3 => "Low",
			< 7 => "Medium",
			< 12 => "High",
			_ => "Critical"
		};
	}

	private static RollbackPlan CreateRollbackPlan()
	{
		return new RollbackPlan
		{
			Strategy = "SnapshotRestore",
			EstimatedDuration = TimeSpan.FromMinutes(10),
			Steps = new List<string>
			{
				"Restore snapshots",
				"Revert definitions",
				"Validate state"
			}
		};
	}

	private async Task StoreMigrationPlanAsync(MigrationPlan plan, IServiceScope scope)
	{
		var db = scope.ServiceProvider.GetRequiredService<BpmnDbContext>();
		var record = await db.MigrationPlans.FindAsync(plan.Id);
		if (record is null)
		{
			db.MigrationPlans.Add(new MigrationPlanRecord { Id = plan.Id, CreatedAt = plan.CreatedAt, Payload = JsonSerializer.Serialize(plan) });
		}
		else
		{
			record.CreatedAt = plan.CreatedAt;
			record.Payload = JsonSerializer.Serialize(plan);
		}

		await db.SaveChangesAsync();
	}

	private static async Task<MigrationPlan> GetMigrationPlanAsync(Guid planId, IServiceScope scope)
	{
		var db = scope.ServiceProvider.GetRequiredService<BpmnDbContext>();
		var record = await db.MigrationPlans.FindAsync(planId);
		if (record is null)
		{
			throw new InvalidOperationException($"Migration plan '{planId}' was not found.");
		}

		return JsonSerializer.Deserialize<MigrationPlan>(record.Payload) ?? throw new InvalidOperationException($"Migration plan '{planId}' contains invalid data.");
	}

	private async Task StoreMigrationExecutionAsync(MigrationExecution execution, IServiceScope scope)
	{
		var db = scope.ServiceProvider.GetRequiredService<BpmnDbContext>();
		var record = await db.MigrationExecutions.FindAsync(execution.Id);
		if (record is null)
		{
			db.MigrationExecutions.Add(new MigrationExecutionRecord { Id = execution.Id, MigrationPlanId = execution.MigrationPlanId, StartedAt = execution.StartedAt, Payload = JsonSerializer.Serialize(execution) });
		}
		else
		{
			record.Payload = JsonSerializer.Serialize(execution);
		}

		await db.SaveChangesAsync();
	}

	private static async Task<MigrationExecution?> GetStoredMigrationExecutionAsync(Guid id, IServiceScope scope)
	{
		var db = scope.ServiceProvider.GetRequiredService<BpmnDbContext>();
		var record = await db.MigrationExecutions.FindAsync(id);
		return record is null ? null : JsonSerializer.Deserialize<MigrationExecution>(record.Payload);
	}


	private static async Task<LiveMigrationSnapshot?> GetStoredSnapshotAsync(Guid id, IServiceScope scope)
	{
		var historyRepository = scope.ServiceProvider.GetRequiredService<IHistoryEventRepository>();
		var historyEvent = await historyRepository.GetByIdAsync(id);
		if (historyEvent is null || historyEvent.EventType != "LIVE_MIGRATION_SNAPSHOT" || string.IsNullOrWhiteSpace(historyEvent.Data))
		{
			return null;
		}

		return JsonSerializer.Deserialize<LiveMigrationSnapshot>(historyEvent.Data);
	}

	private static List<ActivityInfo> ExtractActivities(ProcessDefinitionInfo process)
	{
		if (string.IsNullOrWhiteSpace(process.BpmnXml))
		{
			return new List<ActivityInfo>();
		}

		return ExtractActivityElements(process.BpmnXml).Values.ToList();
	}

	private static List<VariableInfo> ExtractVariables(ProcessDefinitionInfo process)
	{
		if (string.IsNullOrWhiteSpace(process.BpmnXml))
		{
			return new List<VariableInfo>();
		}

		var document = XDocument.Parse(process.BpmnXml);
		return document.Descendants().Where(element => element.Name.LocalName is "property" or "variable").Select(element => new VariableInfo { Name = element.Attribute("name")?.Value ?? string.Empty, Type = element.Attribute("type")?.Value ?? "string" }).Where(variable => !string.IsNullOrWhiteSpace(variable.Name)).ToList();
	}









	private static void EnsureTenantAccess(string? resourceTenantId, string? requestedTenantId)
	{
		if (!string.Equals(resourceTenantId, requestedTenantId, StringComparison.Ordinal))
		{
			throw new UnauthorizedAccessException("The migration resource does not belong to the requested tenant.");
		}
	}
}// Data Models
// Supporting classes
