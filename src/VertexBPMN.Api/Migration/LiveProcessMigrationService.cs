using System.Collections.Concurrent;
using System.Text.Json;
using System.Xml.Linq;
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

    public async Task<MigrationPlan> CreateMigrationPlanAsync(string fromProcessKey, string toProcessKey, MigrationOptions options)
    {
        try
        {
            _logger.LogInformation("Creating migration plan from {FromProcess} to {ToProcess}", fromProcessKey, toProcessKey);

            using var scope = _serviceProvider.CreateScope();
            
            // Get process definitions
            var fromProcess = await GetProcessDefinitionAsync(fromProcessKey, scope);
            var toProcess = await GetProcessDefinitionAsync(toProcessKey, scope);
            
            // Analyze compatibility
            var compatibilityIssues = await ValidateCompatibilityAsync(fromProcessKey, toProcessKey);
            
            // Get active instances
            var activeInstances = await GetActiveProcessInstancesAsync(fromProcessKey, scope);
            
            // Create mapping strategy
            var mappingStrategy = await CreateActivityMappingAsync(fromProcess, toProcess);
            
            // Calculate migration complexity
            var complexity = CalculateMigrationComplexity(activeInstances.Count, compatibilityIssues.Count, mappingStrategy);
            
            var migrationPlan = new MigrationPlan
            {
                Id = Guid.NewGuid(),
                FromProcessKey = fromProcessKey,
                ToProcessKey = toProcessKey,
                Options = options,
                CreatedAt = DateTime.UtcNow,
                EstimatedDuration = CalculateEstimatedDuration(activeInstances.Count, complexity),
                Complexity = complexity,
                CompatibilityIssues = compatibilityIssues,
                ActivityMappings = mappingStrategy,
                AffectedInstances = activeInstances.Count,
                MigrationSteps = GenerateMigrationSteps(fromProcessKey, toProcessKey, mappingStrategy, options),
                RiskAssessment = AssessMigrationRisk(compatibilityIssues, activeInstances.Count),
                RollbackPlan = CreateRollbackPlan(fromProcessKey, toProcessKey)
            };

            // Store migration plan
            await StoreMigrationPlanAsync(migrationPlan, scope);
            
            _logger.LogInformation("Migration plan created: {PlanId}, affecting {InstanceCount} instances", 
                migrationPlan.Id, migrationPlan.AffectedInstances);
            
            return migrationPlan;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating migration plan from {FromProcess} to {ToProcess}", fromProcessKey, toProcessKey);
            throw;
        }
    }

    public async Task<MigrationExecution> ExecuteMigrationAsync(Guid migrationPlanId, bool dryRun = false)
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
            
            var execution = new MigrationExecution
            {
                Id = Guid.NewGuid(),
                MigrationPlanId = migrationPlanId,
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
                // Execute migration steps
                await ExecuteMigrationStepsAsync(execution, migrationPlan, scope);
                
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
                
                if (!dryRun)
                {
                    await RollbackMigrationAsync(execution.Id);
                }
            }

            return execution;
        }
        finally
        {
            _migrationSemaphore.Release();
        }
    }

    public async Task<MigrationStatus> GetMigrationStatusAsync(Guid migrationId)
    {
        if (_activeMigrations.TryGetValue(migrationId, out var execution))
        {
            return execution.Status;
        }

        // Check completed migrations in storage
        using var scope = _serviceProvider.CreateScope();
        var storedExecution = await GetStoredMigrationExecutionAsync(migrationId, scope);
        return storedExecution?.Status ?? MigrationStatus.NotFound;
    }

    public async Task<bool> RollbackMigrationAsync(Guid migrationId)
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
                    await RestoreFromSnapshotAsync(snapshot.ProcessInstanceId, snapshotId);
            }

            execution.Status = MigrationStatus.RolledBack;
            execution.CompletedAt = DateTime.UtcNow;
            using var storeScope = _serviceProvider.CreateScope();
            await StoreMigrationExecutionAsync(execution, storeScope);

            _logger.LogInformation("Rollback completed for migration {MigrationId}", migrationId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during rollback of migration {MigrationId}", migrationId);
            return false;
        }
    }

    public async Task<List<MigrationCompatibilityIssue>> ValidateCompatibilityAsync(string fromProcessKey, string toProcessKey)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            
            var fromProcess = await GetProcessDefinitionAsync(fromProcessKey, scope);
            var toProcess = await GetProcessDefinitionAsync(toProcessKey, scope);
            
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating compatibility between {FromProcess} and {ToProcess}", fromProcessKey, toProcessKey);
            throw;
        }
    }

    public async Task<LiveMigrationSnapshot> CreateSnapshotAsync(Guid processInstanceId)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            
            var processInstance = await GetProcessInstanceAsync(processInstanceId, scope);
            var activeTokens = await GetActiveTokensAsync(processInstanceId, scope);
            var variables = await GetVariablesAsync(processInstanceId, scope);
            
            var snapshot = new LiveMigrationSnapshot
            {
                Id = Guid.NewGuid(),
                ProcessInstanceId = processInstanceId,
                CreatedAt = DateTime.UtcNow,
                ProcessState = JsonSerializer.Serialize(processInstance),
                TokenStates = activeTokens.ToDictionary(t => t.Id.ToString(), t => JsonSerializer.Serialize(t)),
                Variables = variables.ToDictionary(v => v.Key, v => JsonSerializer.Serialize(v.Value)),
                ActivityStates = await CaptureActivityStatesAsync(processInstanceId, scope)
            };

            _snapshots[snapshot.Id] = snapshot;
            await StoreSnapshotAsync(snapshot, scope);

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

    public async Task<bool> RestoreFromSnapshotAsync(Guid processInstanceId, Guid snapshotId)
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

            using var restoreScope = _serviceProvider.CreateScope();
            
            // Restore process state
            var processInstance = JsonSerializer.Deserialize<ProcessInstanceState>(snapshot.ProcessState);
            await RestoreProcessInstanceAsync(processInstance!, restoreScope);
            
            // Restore tokens
            foreach (var tokenEntry in snapshot.TokenStates)
            {
                var token = JsonSerializer.Deserialize<TokenState>(tokenEntry.Value);
                await RestoreTokenAsync(token!, restoreScope);
            }
            
            // Restore variables
            foreach (var variableEntry in snapshot.Variables)
            {
                var value = JsonSerializer.Deserialize<object>(variableEntry.Value);
                await SetVariableAsync(processInstanceId, variableEntry.Key, value!, restoreScope);
            }
            
            _logger.LogInformation("Successfully restored process instance {ProcessInstanceId} from snapshot {SnapshotId}", 
                processInstanceId, snapshotId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error restoring process instance {ProcessInstanceId} from snapshot {SnapshotId}", 
                processInstanceId, snapshotId);
            return false;
        }
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

    private async Task<ProcessDefinitionInfo> GetProcessDefinitionAsync(string processKey, IServiceScope scope)
    {
        var repository = scope.ServiceProvider.GetRequiredService<IProcessDefinitionRepository>();
        var definition = await repository.GetLatestByKeyAsync(processKey);
        if (definition is null)
            throw new InvalidOperationException($"Process definition '{processKey}' was not found.");

        return new ProcessDefinitionInfo
        {
            Key = definition.Key,
            Name = definition.Name,
            Version = definition.Version,
            BpmnXml = definition.BpmnXml
        };
    }

    private async Task<List<ProcessInstanceInfo>> GetActiveProcessInstancesAsync(string processKey, IServiceScope scope)
    {
        var definitionRepository = scope.ServiceProvider.GetRequiredService<IProcessDefinitionRepository>();
        var runtimeService = scope.ServiceProvider.GetRequiredService<IRuntimeService>();
        var definition = await definitionRepository.GetLatestByKeyAsync(processKey);
        if (definition is null)
            throw new InvalidOperationException($"Process definition '{processKey}' was not found.");

        var instances = new List<ProcessInstanceInfo>();
        await foreach (var instance in runtimeService.ListAsync(definition.Id, cancellationToken: CancellationToken.None))
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
            Data = JsonSerializer.Serialize(snapshot)
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

    private List<ActivityInfo> ExtractActivities(ProcessDefinitionInfo process)
    {
        if (string.IsNullOrWhiteSpace(process.BpmnXml))
            return new List<ActivityInfo>();
        var document = XDocument.Parse(process.BpmnXml);
        return document.Descendants()
            .Where(element => element.Attribute("id") is not null &&
                (element.Name.LocalName.EndsWith("Event", StringComparison.Ordinal) ||
                 element.Name.LocalName.EndsWith("Task", StringComparison.Ordinal) ||
                 element.Name.LocalName.EndsWith("Gateway", StringComparison.Ordinal)))
            .Select(element => new ActivityInfo
            {
                Id = element.Attribute("id")!.Value,
                Name = element.Attribute("name")?.Value ?? element.Attribute("id")!.Value,
                Type = element.Name.LocalName
            })
            .ToList();
    }

    private List<VariableInfo> ExtractVariables(ProcessDefinitionInfo process)
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

}

// Data Models

// Supporting classes