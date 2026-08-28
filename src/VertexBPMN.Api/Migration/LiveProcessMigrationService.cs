using System.Collections.Concurrent;
using System.Text.Json;
using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Domain.Interfaces.Repositories;
using VertexBPMN.Infrastructure.Persistence;

namespace VertexBPMN.Api.Migration;

public class LiveProcessMigrationService : ILiveProcessMigrationService
{
    private readonly ILogger<LiveProcessMigrationService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly ConcurrentDictionary<Guid, MigrationExecution> _activeMigrations = new();
    private readonly ConcurrentDictionary<Guid, LiveMigrationSnapshot> _snapshots = new();
    private readonly SemaphoreSlim _migrationSemaphore = new(5); // Max 5 concurrent migrations

    public LiveProcessMigrationService(
        ILogger<LiveProcessMigrationService> logger,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    public async Task<MigrationPlan> CreateMigrationPlanAsync(
        string fromProcessKey,
        string toProcessKey,
        MigrationOptions options,
        string? tenantId = null)
    {
        using var scope = _serviceProvider.CreateScope();
        var fromProcess = await GetProcessDefinitionAsync(fromProcessKey, tenantId, scope);
        var toProcess = await GetProcessDefinitionAsync(toProcessKey, tenantId, scope);
        return await CreateMigrationPlanCoreAsync(fromProcess, toProcess, options, scope);
    }

    public async Task<MigrationPlan> CreateMigrationPlanByDefinitionIdAsync(
        Guid fromProcessDefinitionId,
        Guid toProcessDefinitionId,
        MigrationOptions options,
        string? tenantId = null)
    {
        using var scope = _serviceProvider.CreateScope();
        var fromProcess = await GetProcessDefinitionByIdAsync(fromProcessDefinitionId, tenantId, scope);
        var toProcess = await GetProcessDefinitionByIdAsync(toProcessDefinitionId, tenantId, scope);
        return await CreateMigrationPlanCoreAsync(fromProcess, toProcess, options, scope);
    }

    private async Task<MigrationPlan> CreateMigrationPlanCoreAsync(
        ProcessDefinitionInfo fromProcess,
        ProcessDefinitionInfo toProcess,
        MigrationOptions options,
        IServiceScope scope)
    {
        if (!string.Equals(fromProcess.TenantId, toProcess.TenantId, StringComparison.Ordinal))
            throw new InvalidOperationException("Source and target process definitions must belong to the same tenant.");

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
            MigrationSteps = GenerateMigrationSteps(fromProcess.Key, toProcess.Key, mappingStrategy, options),
            RiskAssessment = AssessMigrationRisk(compatibilityIssues, activeInstances.Count),
            RollbackPlan = CreateRollbackPlan(fromProcess.Key, toProcess.Key)
        };
        await StoreMigrationPlanAsync(migrationPlan, scope);
        _logger.LogInformation(
            "Migration plan {PlanId} binds definition {SourceDefinitionId} to {TargetDefinitionId} for tenant {TenantId} and affects {InstanceCount} instances",
            migrationPlan.Id, fromProcess.Id, toProcess.Id, fromProcess.TenantId, activeInstances.Count);
        return migrationPlan;
    }

    public async Task<MigrationExecution> ExecuteMigrationAsync(
        Guid migrationPlanId,
        bool dryRun = false,
        string? tenantId = null)
    {
        if (!await _migrationSemaphore.WaitAsync(TimeSpan.FromMinutes(5)))
        {
            throw new InvalidOperationException("Migration service is currently at capacity. Please try again later.");
        }

        try
        {
            _logger.LogInformation("Executing migration plan {PlanId} (DryRun: {DryRun})", migrationPlanId, dryRun);

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
                
                _logger.LogInformation("Migration {ExecutionId} completed successfully", execution.Id);
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
        if (storedExecution is not null) EnsureTenantAccess(storedExecution.TenantId, tenantId);
        return storedExecution?.Status ?? MigrationStatus.NotFound;
    }

    public async Task<bool> RollbackMigrationAsync(Guid migrationId, string? tenantId = null)
    {
        try
        {
            _logger.LogInformation("Starting rollback for migration {MigrationId}", migrationId);

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
                    snapshot = cachedSnapshot;
                else
                {
                    using var snapshotScope = _serviceProvider.CreateScope();
                    snapshot = await GetStoredSnapshotAsync(snapshotId, snapshotScope);
                }
                if (snapshot is not null)
                    await RestoreFromSnapshotAsync(snapshot.ProcessInstanceId, snapshotId, tenantId);
            }

            execution.Status = MigrationStatus.RolledBack;
            execution.CompletedAt = DateTime.UtcNow;
            using var storeScope = _serviceProvider.CreateScope();
            await StoreMigrationExecutionAsync(execution, storeScope);

            _logger.LogInformation("Rollback completed for migration {MigrationId}", migrationId);
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

    public async Task<List<MigrationCompatibilityIssue>> ValidateCompatibilityAsync(
        string fromProcessKey,
        string toProcessKey,
        string? tenantId = null)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var fromProcess = await GetProcessDefinitionAsync(fromProcessKey, tenantId, scope);
            var toProcess = await GetProcessDefinitionAsync(toProcessKey, tenantId, scope);
            return ValidateCompatibility(fromProcess, toProcess);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating compatibility between {FromProcess} and {ToProcess}", fromProcessKey, toProcessKey);
            throw;
        }
    }

    private static List<MigrationCompatibilityIssue> ValidateCompatibility(
        ProcessDefinitionInfo fromProcess,
        ProcessDefinitionInfo toProcess)
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
                    issues.Add(new MigrationCompatibilityIssue
                    {
                        Type = "ActivityNotFound",
                        Severity = "High",
                        Description = $"Activity '{fromActivity.Name}' not found in target process",
                        Recommendation = "Add activity mapping or update target process definition",
                        AffectedElement = fromActivity.Id
                    });
                }
                else if (fromActivity.Type != mappedActivity.Type)
                {
                    issues.Add(new MigrationCompatibilityIssue
                    {
                        Type = "ActivityTypeMismatch",
                        Severity = "Medium",
                        Description = $"Activity '{fromActivity.Name}' type changed from {fromActivity.Type} to {mappedActivity.Type}",
                        Recommendation = "Review activity behavior compatibility",
                        AffectedElement = fromActivity.Id
                    });
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
                    issues.Add(new MigrationCompatibilityIssue
                    {
                        Type = "VariableNotFound",
                        Severity = "Medium",
                        Description = $"Variable '{fromVar.Name}' not found in target process",
                        Recommendation = "Add variable or create mapping rule",
                        AffectedElement = fromVar.Name
                    });
                }
                else if (fromVar.Type != mappedVar.Type)
                {
                    issues.Add(new MigrationCompatibilityIssue
                    {
                        Type = "VariableTypeMismatch",
                        Severity = "High",
                        Description = $"Variable '{fromVar.Name}' type changed from {fromVar.Type} to {mappedVar.Type}",
                        Recommendation = "Create type conversion rule",
                        AffectedElement = fromVar.Name
                    });
                }
            }

            return issues;
    }

    public async Task<LiveMigrationSnapshot> CreateSnapshotAsync(Guid processInstanceId, string? tenantId = null)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BpmnDbContext>();
            var instance = await db.ProcessInstances.SingleOrDefaultAsync(item => item.Id == processInstanceId)
                           ?? throw new InvalidOperationException($"Process instance '{processInstanceId}' was not found.");
            EnsureTenantAccess(instance.TenantId, tenantId);
            var snapshot = await CreateSnapshotFromDbAsync(db, instance);
            _snapshots[snapshot.Id] = snapshot;
            await db.SaveChangesAsync();

            _logger.LogDebug("Created snapshot {SnapshotId} for process instance {ProcessInstanceId}", 
                snapshot.Id, processInstanceId);

            return snapshot;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating snapshot for process instance {ProcessInstanceId}", processInstanceId);
            throw;
        }
    }

    private static async Task<LiveMigrationSnapshot> CreateSnapshotFromDbAsync(
        BpmnDbContext db,
        ProcessInstance instance)
    {
        var tokens = await db.ExecutionTokens.AsNoTracking()
            .Where(item => item.ProcessInstanceId == instance.Id).ToArrayAsync();
        var tasks = await db.Tasks.AsNoTracking()
            .Where(item => item.ProcessInstanceId == instance.Id).ToArrayAsync();
        var jobs = await db.Jobs.AsNoTracking()
            .Where(item => item.ProcessInstanceId == instance.Id).ToArrayAsync();
        var subscriptions = await db.EventSubscriptions.AsNoTracking()
            .Where(item => item.ProcessInstanceId == instance.Id).ToArrayAsync();
        var incidents = await db.Incidents.AsNoTracking()
            .Where(item => item.ProcessInstanceId == instance.Id).ToArrayAsync();
        var multiInstances = await db.MultiInstanceExecutions.AsNoTracking()
            .Where(item => item.ProcessInstanceId == instance.Id).ToArrayAsync();
        var variables = await db.Variables.AsNoTracking()
            .Where(item => item.ProcessInstanceId == instance.Id).ToArrayAsync();

        var snapshot = new LiveMigrationSnapshot
        {
            Id = Guid.NewGuid(),
            ProcessInstanceId = instance.Id,
            TenantId = instance.TenantId,
            CreatedAt = DateTime.UtcNow,
            ProcessState = JsonSerializer.Serialize(new ProcessInstanceState
            {
                Id = instance.Id,
                Status = instance.Status.ToString(),
                State = instance.State,
                ProcessDefinitionId = instance.ProcessDefinitionId,
                ProcessId = instance.ProcessId,
                ActiveTokens = [.. instance.ActiveTokens],
                ActiveTasks = [.. instance.ActiveTasks]
            }),
            TokenStates = tokens.ToDictionary(
                token => token.Id.ToString(),
                token => JsonSerializer.Serialize(new TokenState
                {
                    Id = token.Id,
                    ProcessInstanceId = token.ProcessInstanceId,
                    ActivityId = token.CurrentNodeId,
                    NodeType = token.NodeType,
                    State = token.State,
                    Variables = token.Variables,
                    CreatedAt = token.CreatedAt,
                    AssignedWorker = token.AssignedWorker,
                    AssignedAt = token.AssignedAt,
                    RetryCount = token.RetryCount
                })),
            Variables = variables.ToDictionary(
                variable => variable.Name,
                variable => JsonSerializer.Serialize(variable.Value)),
            ActivityStates = tokens.GroupBy(token => token.CurrentNodeId, StringComparer.Ordinal)
                .ToDictionary(
                group => group.Key,
                group => JsonSerializer.Serialize(new ActivityState
                {
                    Id = group.Key,
                    Status = string.Join(",", group.Select(token => token.State).Distinct())
                }), StringComparer.Ordinal),
            TaskActivityIds = tasks.ToDictionary(item => item.Id, item => item.ActivityId),
            JobActivityIds = jobs.ToDictionary(item => item.Id, item => item.ActivityId),
            SubscriptionActivityIds = subscriptions.ToDictionary(item => item.Id, item => item.ActivityId),
            SubscriptionActiveKeys = subscriptions.ToDictionary(item => item.Id, item => item.ActiveKey),
            IncidentActivityIds = incidents.ToDictionary(item => item.Id, item => item.ActivityId),
            MultiInstanceActivityIds = multiInstances.ToDictionary(item => item.Id, item => item.ActivityId)
        };
        db.HistoryEvents.Add(new HistoryEvent
        {
            Id = snapshot.Id,
            ProcessInstanceId = snapshot.ProcessInstanceId,
            EventType = "LIVE_MIGRATION_SNAPSHOT",
            Timestamp = snapshot.CreatedAt,
            Details = "Live process migration snapshot",
            ElementId = snapshot.ProcessInstanceId.ToString(),
            Data = JsonSerializer.Serialize(snapshot),
            TenantId = instance.TenantId
        });
        return snapshot;
    }

    public async Task<bool> RestoreFromSnapshotAsync(
        Guid processInstanceId,
        Guid snapshotId,
        string? tenantId = null)
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
            var state = JsonSerializer.Deserialize<ProcessInstanceState>(snapshot.ProcessState)
                        ?? throw new InvalidOperationException($"Snapshot '{snapshotId}' has invalid process state.");
            if (state.Id != processInstanceId || snapshot.ProcessInstanceId != processInstanceId)
                throw new InvalidOperationException("Snapshot does not belong to the requested process instance.");
            var instance = await db.ProcessInstances.SingleAsync(item => item.Id == processInstanceId);
            EnsureTenantAccess(instance.TenantId, tenantId);
            instance.ProcessDefinitionId = state.ProcessDefinitionId;
            instance.ProcessId = state.ProcessId;
            instance.State = state.State;
            if (Enum.TryParse<ProcessInstanceStatus>(state.Status, out var status)) instance.Status = status;
            instance.ActiveTokens = [.. state.ActiveTokens];
            instance.ActiveTasks = [.. state.ActiveTasks];
            instance.LastModified = DateTime.UtcNow;
            instance.Revision++;

            foreach (var tokenStateJson in snapshot.TokenStates.Values)
            {
                var tokenState = JsonSerializer.Deserialize<TokenState>(tokenStateJson)
                                 ?? throw new InvalidOperationException("Snapshot contains an invalid token.");
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
            foreach (var (id, activityId) in snapshot.TaskActivityIds)
            {
                var task = await db.Tasks.SingleAsync(item => item.Id == id);
                task.ActivityId = activityId;
                task.LastModified = DateTime.UtcNow;
                task.Revision++;
            }
            foreach (var (id, activityId) in snapshot.JobActivityIds)
            {
                var job = await db.Jobs.SingleAsync(item => item.Id == id);
                job.ActivityId = activityId;
                job.Revision++;
            }
            foreach (var (id, activityId) in snapshot.SubscriptionActivityIds)
            {
                var subscription = await db.EventSubscriptions.SingleAsync(item => item.Id == id);
                subscription.ActivityId = activityId;
                subscription.ActiveKey = snapshot.SubscriptionActiveKeys.GetValueOrDefault(id);
                subscription.Revision++;
            }
            foreach (var (id, activityId) in snapshot.IncidentActivityIds)
                (await db.Incidents.SingleAsync(item => item.Id == id)).ActivityId = activityId;
            foreach (var (id, activityId) in snapshot.MultiInstanceActivityIds)
            {
                var multiInstance = await db.MultiInstanceExecutions.SingleAsync(item => item.Id == id);
                multiInstance.ActivityId = activityId;
                multiInstance.Revision++;
            }

            db.RuntimeOutbox.Add(new RuntimeOutboxMessage
            {
                Id = Guid.NewGuid(),
                ProcessInstanceId = instance.Id,
                EventType = "ProcessMigrationRolledBack",
                TenantId = instance.TenantId,
                OccurredAt = DateTime.UtcNow,
                Payload = JsonSerializer.Serialize(new { snapshotId })
            });
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
            
            _logger.LogInformation("Successfully restored process instance {ProcessInstanceId} from snapshot {SnapshotId}", 
                processInstanceId, snapshotId);

            return true;
        }
        catch (UnauthorizedAccessException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error restoring process instance {ProcessInstanceId} from snapshot {SnapshotId}", 
                processInstanceId, snapshotId);
            return false;
        }
    }

    private async Task ExecuteTransactionalMigrationAsync(
        MigrationExecution execution,
        MigrationPlan plan,
        IServiceScope scope)
    {
        var db = scope.ServiceProvider.GetRequiredService<BpmnDbContext>();
        var sourceDefinitions = db.ProcessDefinitions
            .Where(definition => definition.TenantId == plan.TenantId);
        var sourceDefinition = plan.FromProcessDefinitionId != Guid.Empty
            ? await sourceDefinitions.SingleOrDefaultAsync(definition => definition.Id == plan.FromProcessDefinitionId)
            : await sourceDefinitions.Where(definition => definition.Key == plan.FromProcessKey)
                .OrderByDescending(definition => definition.Version).FirstOrDefaultAsync();
        if (sourceDefinition is null)
            throw new InvalidOperationException(
                $"Source process definition '{plan.FromProcessDefinitionId}' was not found in tenant '{plan.TenantId}'.");
        var targetDefinitions = db.ProcessDefinitions
            .Where(definition => definition.TenantId == plan.TenantId);
        var targetDefinition = plan.ToProcessDefinitionId != Guid.Empty
            ? await targetDefinitions.SingleOrDefaultAsync(definition => definition.Id == plan.ToProcessDefinitionId)
            : await targetDefinitions.Where(definition => definition.Key == plan.ToProcessKey)
                .OrderByDescending(definition => definition.Version).FirstOrDefaultAsync();
        if (targetDefinition is null)
            throw new InvalidOperationException(
                $"Target process definition '{plan.ToProcessDefinitionId}' was not found in tenant '{plan.TenantId}'.");

        var targetActivities = ExtractActivityElements(targetDefinition.BpmnXml);
        var mappings = plan.ActivityMappings
            .Where(mapping => !string.IsNullOrWhiteSpace(mapping.ToActivityId))
            .ToDictionary(mapping => mapping.FromActivityId, mapping => mapping.ToActivityId,
                StringComparer.Ordinal);
        string Map(string activityId)
        {
            var targetId = mappings.GetValueOrDefault(activityId)
                           ?? (targetActivities.ContainsKey(activityId) ? activityId : null);
            if (string.IsNullOrWhiteSpace(targetId) || !targetActivities.ContainsKey(targetId))
                throw new InvalidOperationException(
                    $"Active activity '{activityId}' has no valid mapping in target process '{plan.ToProcessKey}'.");
            return targetId;
        }

        var instances = await db.ProcessInstances
            .Where(instance => instance.ProcessDefinitionId == sourceDefinition.Id
                               && instance.Status != ProcessInstanceStatus.Completed
                               && instance.Status != ProcessInstanceStatus.Terminated)
            .OrderBy(instance => instance.CreatedAt)
            .ToArrayAsync();
        if (instances.Length != plan.AffectedInstances)
            throw new DbUpdateConcurrencyException(
                $"Migration plan expected {plan.AffectedInstances} active instances but found {instances.Length}.");

        // Validate every current wait-state before entering the write transaction.
        foreach (var instance in instances)
        {
            var activeIds = await ActiveActivityIdsAsync(db, instance.Id);
            foreach (var activityId in activeIds) _ = Map(activityId);
        }

        execution.Steps.Add(new MigrationStepResult
        {
            StepName = "Validate active runtime mappings",
            StartedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow,
            Status = "Completed"
        });
        execution.Progress = execution.IsDryRun ? 100 : 25;
        if (execution.IsDryRun) return;

        await using var transaction = await db.Database.BeginTransactionAsync();
        foreach (var instance in instances)
        {
            execution.AffectedProcessInstanceIds.Add(instance.Id);
            var snapshot = await CreateSnapshotFromDbAsync(db, instance);
            execution.Snapshots.Add(snapshot.Id);
            _snapshots[snapshot.Id] = snapshot;

            var tokens = await db.ExecutionTokens
                .Where(item => item.ProcessInstanceId == instance.Id).ToArrayAsync();
            var tasks = await db.Tasks
                .Where(item => item.ProcessInstanceId == instance.Id
                               && (item.Status == UserTaskStatus.Pending || item.Status == UserTaskStatus.Delegated))
                .ToArrayAsync();
            var jobs = await db.Jobs
                .Where(item => item.ProcessInstanceId == instance.Id && item.State == "Scheduled")
                .ToArrayAsync();
            var subscriptions = await db.EventSubscriptions
                .Where(item => item.ProcessInstanceId == instance.Id && item.State == "Active")
                .ToArrayAsync();
            var incidents = await db.Incidents
                .Where(item => item.ProcessInstanceId == instance.Id && item.State != "Resolved")
                .ToArrayAsync();
            var multiInstances = await db.MultiInstanceExecutions
                .Where(item => item.ProcessInstanceId == instance.Id && item.State == "Active")
                .ToArrayAsync();

            foreach (var token in tokens)
            {
                token.CurrentNodeId = Map(token.CurrentNodeId);
                token.Revision++;
            }
            foreach (var task in tasks)
            {
                task.ActivityId = Map(task.ActivityId);
                task.Name = targetActivities[task.ActivityId].Name;
                task.LastModified = DateTime.UtcNow;
                task.Revision++;
            }
            foreach (var job in jobs)
            {
                job.ActivityId = Map(job.ActivityId);
                job.Revision++;
            }
            foreach (var subscription in subscriptions)
            {
                subscription.ActivityId = Map(subscription.ActivityId);
                subscription.ActiveKey = $"{instance.Id:N}:{subscription.ActivityId}";
                subscription.Revision++;
            }
            foreach (var incident in incidents)
                if (!string.IsNullOrWhiteSpace(incident.ActivityId))
                    incident.ActivityId = Map(incident.ActivityId);
            foreach (var multiInstance in multiInstances)
            {
                multiInstance.ActivityId = Map(multiInstance.ActivityId);
                multiInstance.Revision++;
            }

            instance.ProcessDefinitionId = targetDefinition.Id;
            instance.ProcessId = targetDefinition.Key;
            instance.ActiveTokens = instance.ActiveTokens.Select(Map).Distinct(StringComparer.Ordinal).ToList();
            instance.ActiveTasks = instance.ActiveTasks.Select(Map).Distinct(StringComparer.Ordinal).ToList();
            instance.LastModified = DateTime.UtcNow;
            instance.Revision++;
            db.RuntimeOutbox.Add(new RuntimeOutboxMessage
            {
                Id = Guid.NewGuid(),
                ProcessInstanceId = instance.Id,
                EventType = "ProcessMigrated",
                TenantId = instance.TenantId,
                OccurredAt = DateTime.UtcNow,
                Payload = JsonSerializer.Serialize(new
                {
                    execution.Id,
                    fromDefinitionId = sourceDefinition.Id,
                    toDefinitionId = targetDefinition.Id
                })
            });
        }

        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        execution.Steps.Add(new MigrationStepResult
        {
            StepName = "Transactionally migrate runtime state",
            StartedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow,
            Status = "Completed"
        });
        execution.Progress = 100;
    }

    private static async Task<HashSet<string>> ActiveActivityIdsAsync(BpmnDbContext db, Guid processInstanceId)
    {
        var activityIds = new HashSet<string>(StringComparer.Ordinal);
        activityIds.UnionWith(await db.ExecutionTokens
            .Where(item => item.ProcessInstanceId == processInstanceId && item.State != "Completed")
            .Select(item => item.CurrentNodeId).ToArrayAsync());
        activityIds.UnionWith(await db.Tasks
            .Where(item => item.ProcessInstanceId == processInstanceId
                           && (item.Status == UserTaskStatus.Pending || item.Status == UserTaskStatus.Delegated))
            .Select(item => item.ActivityId).ToArrayAsync());
        activityIds.UnionWith(await db.Jobs
            .Where(item => item.ProcessInstanceId == processInstanceId && item.State == "Scheduled")
            .Select(item => item.ActivityId).ToArrayAsync());
        activityIds.UnionWith(await db.EventSubscriptions
            .Where(item => item.ProcessInstanceId == processInstanceId && item.State == "Active")
            .Select(item => item.ActivityId).ToArrayAsync());
        activityIds.UnionWith(await db.Incidents
            .Where(item => item.ProcessInstanceId == processInstanceId
                           && item.State != "Resolved" && item.ActivityId != null)
            .Select(item => item.ActivityId!).ToArrayAsync());
        activityIds.UnionWith(await db.MultiInstanceExecutions
            .Where(item => item.ProcessInstanceId == processInstanceId && item.State == "Active")
            .Select(item => item.ActivityId).ToArrayAsync());
        return activityIds;
    }

    private static Dictionary<string, ActivityInfo> ExtractActivityElements(string bpmnXml)
    {
        var document = XDocument.Parse(bpmnXml, LoadOptions.PreserveWhitespace);
        return document.Descendants()
            .Where(element => element.Attribute("id") is not null
                              && (element.Name.LocalName.EndsWith("Event", StringComparison.Ordinal)
                                  || element.Name.LocalName.EndsWith("Task", StringComparison.Ordinal)
                                  || element.Name.LocalName.EndsWith("Gateway", StringComparison.Ordinal)
                                  || element.Name.LocalName is "subProcess" or "transaction" or "callActivity"))
            .ToDictionary(
                element => element.Attribute("id")!.Value,
                element => new ActivityInfo
                {
                    Id = element.Attribute("id")!.Value,
                    Name = element.Attribute("name")?.Value ?? element.Attribute("id")!.Value,
                    Type = element.Name.LocalName
                },
                StringComparer.Ordinal);
    }

    // Helper methods
    private async Task ExecuteMigrationStepsAsync(MigrationExecution execution, MigrationPlan plan, IServiceScope scope)
    {
        var totalSteps = plan.MigrationSteps.Count;
        var completedSteps = 0;

        foreach (var step in plan.MigrationSteps)
        {
            var stepResult = new MigrationStepResult
            {
                StepName = step.Name,
                StartedAt = DateTime.UtcNow,
                Status = "InProgress"
            };

            try
            {
                switch (step.Type)
                {
                    case "CreateSnapshots":
                        await ExecuteCreateSnapshotsStep(execution, step, scope);
                        break;
                    case "MigrateInstances":
                        await ExecuteMigrateInstancesStep(execution, step, scope);
                        break;
                    case "UpdateDefinitions":
                    case "UpdateDefinitionsStep":
                        await ExecuteUpdateDefinitionsStep(execution, step, scope);
                        break;
                    case "ValidateResults":
                        await ExecuteValidateResultsStep(execution, step, scope);
                        break;
                    default:
                        throw new NotSupportedException($"Migration step type '{step.Type}' is not supported");
                }

                stepResult.Status = "Completed";
                stepResult.CompletedAt = DateTime.UtcNow;
                completedSteps++;
                execution.Progress = (int)((double)completedSteps / totalSteps * 100);
            }
            catch (Exception ex)
            {
                stepResult.Status = "Failed";
                stepResult.Error = ex.Message;
                stepResult.CompletedAt = DateTime.UtcNow;
                throw;
            }
            finally
            {
                execution.Steps.Add(stepResult);
            }
        }
    }

    private async Task ExecuteCreateSnapshotsStep(MigrationExecution execution, MigrationStep step, IServiceScope scope)
    {
        var activeInstances = await GetActiveProcessInstancesAsync(step.Parameters["processKey"].ToString()!, scope);
        
        foreach (var instance in activeInstances)
        {
            if (!execution.IsDryRun)
            {
                var snapshot = await CreateSnapshotAsync(instance.Id);
                execution.Snapshots.Add(snapshot.Id);
            }
        }
    }

    private async Task ExecuteMigrateInstancesStep(MigrationExecution execution, MigrationStep step, IServiceScope scope)
    {
        var fromProcessKey = step.Parameters["fromProcessKey"].ToString()!;
        var toProcessKey = step.Parameters["toProcessKey"].ToString()!;
        var definitionRepository = scope.ServiceProvider.GetRequiredService<IProcessDefinitionRepository>();
        var instanceRepository = scope.ServiceProvider.GetRequiredService<IProcessInstanceRepository>();
        var targetDefinition = await definitionRepository.GetLatestByKeyAsync(toProcessKey);
        if (targetDefinition is null)
            throw new InvalidOperationException($"Target process definition '{toProcessKey}' was not found.");

        var activeInstances = await GetActiveProcessInstancesAsync(fromProcessKey, scope);
        foreach (var activeInstance in activeInstances)
        {
            if (execution.IsDryRun)
                continue;

            var instance = await instanceRepository.GetByIdAsync(activeInstance.Id);
            if (instance is null)
                throw new InvalidOperationException($"Process instance '{activeInstance.Id}' was not found.");

            instance.ProcessDefinitionId = targetDefinition.Id;
            instance.ProcessId = targetDefinition.Key;
            await instanceRepository.UpdateAsync(instance);
        }
    }

    private async Task ExecuteUpdateDefinitionsStep(MigrationExecution execution, MigrationStep step, IServiceScope scope)
    {
        var toProcessKey = step.Parameters["toProcessKey"].ToString()!;
        var definitionRepository = scope.ServiceProvider.GetRequiredService<IProcessDefinitionRepository>();
        if (await definitionRepository.GetLatestByKeyAsync(toProcessKey) is null)
            throw new InvalidOperationException($"Target process definition '{toProcessKey}' was not found.");
    }

    private async Task ExecuteValidateResultsStep(MigrationExecution execution, MigrationStep step, IServiceScope scope)
    {
        var fromProcessKey = step.Parameters.TryGetValue("fromProcessKey", out var fromKey) ? fromKey?.ToString() : null;
        var toProcessKey = step.Parameters.TryGetValue("toProcessKey", out var toKey) ? toKey?.ToString() : null;
        if (!string.IsNullOrWhiteSpace(fromProcessKey) && !string.IsNullOrWhiteSpace(toProcessKey))
        {
            var remainingIssues = await ValidateCompatibilityAsync(fromProcessKey, toProcessKey);
            if (remainingIssues.Any(issue => issue.Severity == "High"))
                throw new InvalidOperationException("Migration validation found high-severity compatibility issues.");
        }
    }

    private async Task<ProcessDefinitionInfo> GetProcessDefinitionAsync(
        string processKey,
        string? tenantId,
        IServiceScope scope)
    {
        var repository = scope.ServiceProvider.GetRequiredService<IProcessDefinitionRepository>();
        var definition = await repository.GetLatestByKeyAsync(processKey, tenantId);
        if (definition is null)
            throw new InvalidOperationException($"Process definition '{processKey}' was not found in tenant '{tenantId}'.");

        return ToProcessDefinitionInfo(definition);
    }

    private async Task<ProcessDefinitionInfo> GetProcessDefinitionByIdAsync(
        Guid processDefinitionId,
        string? tenantId,
        IServiceScope scope)
    {
        var repository = scope.ServiceProvider.GetRequiredService<IProcessDefinitionRepository>();
        var definition = await repository.GetByIdAsync(processDefinitionId);
        if (definition is null || !string.Equals(definition.TenantId, tenantId, StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"Process definition '{processDefinitionId}' was not found in tenant '{tenantId}'.");

        return ToProcessDefinitionInfo(definition);
    }

    private static ProcessDefinitionInfo ToProcessDefinitionInfo(ProcessDefinition definition) =>
        new()
        {
            Id = definition.Id,
            TenantId = definition.TenantId,
            Key = definition.Key,
            Name = definition.Name,
            Version = definition.Version,
            BpmnXml = definition.BpmnXml
        };

    private async Task<List<ProcessInstanceInfo>> GetActiveProcessInstancesAsync(
        Guid processDefinitionId,
        string processKey,
        IServiceScope scope)
    {
        var runtimeService = scope.ServiceProvider.GetRequiredService<IRuntimeService>();
        return await CollectActiveInstancesAsync(runtimeService, processDefinitionId, processKey);
    }

    private async Task<List<ProcessInstanceInfo>> GetActiveProcessInstancesAsync(string processKey, IServiceScope scope)
    {
        var definitionRepository = scope.ServiceProvider.GetRequiredService<IProcessDefinitionRepository>();
        var runtimeService = scope.ServiceProvider.GetRequiredService<IRuntimeService>();
        var definition = await definitionRepository.GetLatestByKeyAsync(processKey);
        if (definition is null)
            throw new InvalidOperationException($"Process definition '{processKey}' was not found.");

        return await CollectActiveInstancesAsync(runtimeService, definition.Id, processKey);
    }

    private static async Task<List<ProcessInstanceInfo>> CollectActiveInstancesAsync(
        IRuntimeService runtimeService,
        Guid processDefinitionId,
        string processKey)
    {
        var instances = new List<ProcessInstanceInfo>();
        await foreach (var instance in runtimeService.ListAsync(processDefinitionId, cancellationToken: CancellationToken.None))
        {
            if (instance.Status is ProcessInstanceStatus.Completed or ProcessInstanceStatus.Terminated)
                continue;

            instances.Add(new ProcessInstanceInfo
            {
                Id = instance.Id,
                ProcessDefinitionKey = processKey,
                Status = instance.Status.ToString()
            });
        }

        return instances;
    }

    private async Task<List<ActivityMappingRule>> CreateActivityMappingAsync(ProcessDefinitionInfo from, ProcessDefinitionInfo to)
    {
        var targetActivities = ExtractActivities(to);
        return ExtractActivities(from)
            .Select(source =>
            {
                var target = targetActivities.FirstOrDefault(candidate => candidate.Id == source.Id || candidate.Name == source.Name);
                return new ActivityMappingRule
                {
                    FromActivityId = source.Id,
                    ToActivityId = target?.Id ?? string.Empty,
                    MappingType = target is null ? "Unmapped" : target.Type == source.Type ? "Direct" : "Transform"
                };
            })
            .ToList();
    }

    private string CalculateMigrationComplexity(int instanceCount, int issueCount, List<ActivityMappingRule> mappings)
    {
        var score = instanceCount * 0.1 + issueCount * 0.5 + mappings.Count(m => m.MappingType != "Direct") * 0.3;
        return score switch
        {
            < 2 => "Low",
            < 5 => "Medium",
            < 10 => "High",
            _ => "Critical"
        };
    }

    private TimeSpan CalculateEstimatedDuration(int instanceCount, string complexity)
    {
        var baseMinutes = complexity switch
        {
            "Low" => 5,
            "Medium" => 15,
            "High" => 30,
            "Critical" => 60,
            _ => 10
        };
        
        return TimeSpan.FromMinutes(baseMinutes + instanceCount * 0.5);
    }

    private List<MigrationStep> GenerateMigrationSteps(
        string fromProcessKey,
        string toProcessKey,
        List<ActivityMappingRule> mappings,
        MigrationOptions options)
    {
        return new List<MigrationStep>
        {
            new() { Name = "Create Snapshots", Type = "CreateSnapshots", Order = 1, Parameters = new Dictionary<string, object> { ["processKey"] = fromProcessKey } },
            new() { Name = "Migrate Instances", Type = "MigrateInstances", Order = 2, Parameters = new Dictionary<string, object> { ["fromProcessKey"] = fromProcessKey, ["toProcessKey"] = toProcessKey } },
            new() { Name = "Update Definitions", Type = "UpdateDefinitionsStep", Order = 3, Parameters = new Dictionary<string, object> { ["toProcessKey"] = toProcessKey } },
            new() { Name = "Validate Results", Type = "ValidateResults", Order = 4, Parameters = new Dictionary<string, object> { ["fromProcessKey"] = fromProcessKey, ["toProcessKey"] = toProcessKey } }
        };
    }

    private string AssessMigrationRisk(List<MigrationCompatibilityIssue> issues, int instanceCount)
    {
        var highSeverityIssues = issues.Count(i => i.Severity == "High");
        var riskScore = highSeverityIssues * 3 + issues.Count * 1 + (instanceCount > 10 ? 2 : 0);
        
        return riskScore switch
        {
            < 3 => "Low",
            < 7 => "Medium",
            < 12 => "High",
            _ => "Critical"
        };
    }

    private RollbackPlan CreateRollbackPlan(string fromProcessKey, string toProcessKey)
    {
        return new RollbackPlan
        {
            Strategy = "SnapshotRestore",
            EstimatedDuration = TimeSpan.FromMinutes(10),
            Steps = new List<string> { "Restore snapshots", "Revert definitions", "Validate state" }
        };
    }

    private async Task StoreMigrationPlanAsync(MigrationPlan plan, IServiceScope scope)
    {
        var db = scope.ServiceProvider.GetRequiredService<BpmnDbContext>();
        var record = await db.MigrationPlans.FindAsync(plan.Id);
        if (record is null)
            db.MigrationPlans.Add(new MigrationPlanRecord { Id = plan.Id, CreatedAt = plan.CreatedAt, Payload = JsonSerializer.Serialize(plan) });
        else
        {
            record.CreatedAt = plan.CreatedAt;
            record.Payload = JsonSerializer.Serialize(plan);
        }
        await db.SaveChangesAsync();
    }

    private async Task<MigrationPlan> GetMigrationPlanAsync(Guid planId, IServiceScope scope)
    {
        var db = scope.ServiceProvider.GetRequiredService<BpmnDbContext>();
        var record = await db.MigrationPlans.FindAsync(planId);
        if (record is null)
            throw new InvalidOperationException($"Migration plan '{planId}' was not found.");
        return JsonSerializer.Deserialize<MigrationPlan>(record.Payload)
            ?? throw new InvalidOperationException($"Migration plan '{planId}' contains invalid data.");
    }

    private async Task StoreMigrationExecutionAsync(MigrationExecution execution, IServiceScope scope)
    {
        var db = scope.ServiceProvider.GetRequiredService<BpmnDbContext>();
        var record = await db.MigrationExecutions.FindAsync(execution.Id);
        if (record is null)
        {
            db.MigrationExecutions.Add(new MigrationExecutionRecord
            {
                Id = execution.Id,
                MigrationPlanId = execution.MigrationPlanId,
                StartedAt = execution.StartedAt,
                Payload = JsonSerializer.Serialize(execution)
            });
        }
        else
        {
            record.Payload = JsonSerializer.Serialize(execution);
        }
        await db.SaveChangesAsync();
    }

    private async Task<MigrationExecution?> GetStoredMigrationExecutionAsync(Guid id, IServiceScope scope)
    {
        var db = scope.ServiceProvider.GetRequiredService<BpmnDbContext>();
        var record = await db.MigrationExecutions.FindAsync(id);
        return record is null ? null : JsonSerializer.Deserialize<MigrationExecution>(record.Payload);
    }
    private async Task StoreSnapshotAsync(LiveMigrationSnapshot snapshot, IServiceScope scope)
    {
        var historyRepository = scope.ServiceProvider.GetRequiredService<IHistoryEventRepository>();
        await historyRepository.AddAsync(new HistoryEvent
        {
            Id = snapshot.Id,
            ProcessInstanceId = snapshot.ProcessInstanceId,
            EventType = "LIVE_MIGRATION_SNAPSHOT",
            Timestamp = snapshot.CreatedAt,
            Details = "Live process migration snapshot",
            ElementId = snapshot.ProcessInstanceId.ToString(),
            Data = JsonSerializer.Serialize(snapshot),
            TenantId = snapshot.TenantId
        });
    }

    private async Task<LiveMigrationSnapshot?> GetStoredSnapshotAsync(Guid id, IServiceScope scope)
    {
        var historyRepository = scope.ServiceProvider.GetRequiredService<IHistoryEventRepository>();
        var historyEvent = await historyRepository.GetByIdAsync(id);
        if (historyEvent is null || historyEvent.EventType != "LIVE_MIGRATION_SNAPSHOT" || string.IsNullOrWhiteSpace(historyEvent.Data))
            return null;

        return JsonSerializer.Deserialize<LiveMigrationSnapshot>(historyEvent.Data);
    }

    private static List<ActivityInfo> ExtractActivities(ProcessDefinitionInfo process)
    {
        if (string.IsNullOrWhiteSpace(process.BpmnXml))
            return new List<ActivityInfo>();
        return ExtractActivityElements(process.BpmnXml).Values.ToList();
    }

    private static List<VariableInfo> ExtractVariables(ProcessDefinitionInfo process)
    {
        if (string.IsNullOrWhiteSpace(process.BpmnXml))
            return new List<VariableInfo>();
        var document = XDocument.Parse(process.BpmnXml);
        return document.Descendants()
            .Where(element => element.Name.LocalName is "property" or "variable")
            .Select(element => new VariableInfo
            {
                Name = element.Attribute("name")?.Value ?? string.Empty,
                Type = element.Attribute("type")?.Value ?? "string"
            })
            .Where(variable => !string.IsNullOrWhiteSpace(variable.Name))
            .ToList();
    }

    private async Task<ProcessInstanceState> GetProcessInstanceAsync(Guid id, IServiceScope scope)
    {
        var repository = scope.ServiceProvider.GetRequiredService<IProcessInstanceRepository>();
        var instance = await repository.GetByIdAsync(id)
            ?? throw new InvalidOperationException($"Process instance '{id}' was not found.");
        return new ProcessInstanceState
        {
            Id = instance.Id,
            Status = instance.Status.ToString(),
            State = instance.State,
            ProcessDefinitionId = instance.ProcessDefinitionId,
            ProcessId = instance.ProcessId
        };
    }

    private async Task<List<TokenState>> GetActiveTokensAsync(Guid processInstanceId, IServiceScope scope)
    {
        var repository = scope.ServiceProvider.GetRequiredService<IExecutionTokenRepository>();
        var tokens = new List<TokenState>();
        await foreach (var token in repository.ListByProcessInstanceAsync(processInstanceId))
        {
            tokens.Add(new TokenState
            {
                Id = token.Id,
                ProcessInstanceId = token.ProcessInstanceId,
                ActivityId = token.CurrentNodeId,
                NodeType = token.NodeType,
                State = token.State
            });
        }
        return tokens;
    }

    private async Task<Dictionary<string, object>> GetVariablesAsync(Guid processInstanceId, IServiceScope scope)
    {
        var repository = scope.ServiceProvider.GetRequiredService<IVariableRepository>();
        var variables = new Dictionary<string, object>();
        await foreach (var variable in repository.ListByScopeAsync(processInstanceId))
            variables[variable.Name] = variable.Value ?? string.Empty;
        return variables;
    }

    private async Task<Dictionary<string, string>> CaptureActivityStatesAsync(Guid processInstanceId, IServiceScope scope)
    {
        var repository = scope.ServiceProvider.GetRequiredService<IExecutionTokenRepository>();
        var states = new Dictionary<string, string>();
        await foreach (var token in repository.ListByProcessInstanceAsync(processInstanceId))
            states[token.CurrentNodeId] = JsonSerializer.Serialize(new ActivityState { Id = token.CurrentNodeId, Status = token.State ?? "Active" });
        return states;
    }

    private async Task RestoreProcessInstanceAsync(ProcessInstanceState state, IServiceScope scope)
    {
        var repository = scope.ServiceProvider.GetRequiredService<IProcessInstanceRepository>();
        var instance = await repository.GetByIdAsync(state.Id)
            ?? throw new InvalidOperationException($"Process instance '{state.Id}' was not found.");
        instance.State = state.State;
        instance.ProcessDefinitionId = state.ProcessDefinitionId;
        instance.ProcessId = state.ProcessId;
        if (Enum.TryParse<ProcessInstanceStatus>(state.Status, out var status))
            instance.Status = status;
        await repository.UpdateAsync(instance);
    }

    private async Task RestoreTokenAsync(TokenState token, IServiceScope scope)
    {
        var repository = scope.ServiceProvider.GetRequiredService<IExecutionTokenRepository>();
        var existing = await repository.GetByIdAsync(token.Id);
        if (existing is not null)
            await repository.DeleteAsync(token.Id);
        await repository.AddAsync(new ExecutionToken(token.Id, token.ProcessInstanceId, token.ActivityId, token.NodeType)
        {
            State = token.State
        });
    }

    private async Task SetVariableAsync(Guid processInstanceId, string name, object value, IServiceScope scope)
    {
        var repository = scope.ServiceProvider.GetRequiredService<IVariableRepository>();
        var existing = await FindVariableAsync(repository, processInstanceId, name);
        await repository.UpsertAsync(new Variable
        {
            Id = existing?.Id ?? Guid.NewGuid(),
            ScopeId = processInstanceId,
            ProcessInstanceId = processInstanceId,
            Name = name,
            Type = value?.GetType().Name ?? "object",
            Value = value is JsonElement element ? element.ToString() : value?.ToString()
        });
    }

    private static async Task<Variable?> FindVariableAsync(IVariableRepository repository, Guid processInstanceId, string name)
    {
        await foreach (var variable in repository.ListByScopeAsync(processInstanceId))
        {
            if (variable.Name == name)
                return variable;
        }
        return null;
    }

    private static void EnsureTenantAccess(string? resourceTenantId, string? requestedTenantId)
    {
        if (!string.Equals(resourceTenantId, requestedTenantId, StringComparison.Ordinal))
            throw new UnauthorizedAccessException("The migration resource does not belong to the requested tenant.");
    }

}

// Data Models

// Supporting classes
