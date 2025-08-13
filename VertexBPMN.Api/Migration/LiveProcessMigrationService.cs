using System.Collections.Concurrent;
using System.Text.Json;

namespace VertexBPMN.Api.Migration;

/// <summary>
/// Live Process Migration System
/// Olympic-level feature: Innovation Differentiators - Live Process Migration
/// </summary>
public interface ILiveProcessMigrationService
{
    Task<MigrationPlan> CreateMigrationPlanAsync(string fromProcessKey, string toProcessKey, MigrationOptions options);
    Task<MigrationExecution> ExecuteMigrationAsync(Guid migrationPlanId, bool dryRun = false);
    Task<MigrationStatus> GetMigrationStatusAsync(Guid migrationId);
    Task<bool> RollbackMigrationAsync(Guid migrationId);
    Task<List<MigrationCompatibilityIssue>> ValidateCompatibilityAsync(string fromProcessKey, string toProcessKey);
    Task<LiveMigrationSnapshot> CreateSnapshotAsync(Guid processInstanceId);
    Task<bool> RestoreFromSnapshotAsync(Guid processInstanceId, Guid snapshotId);
}

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
                MigrationSteps = GenerateMigrationSteps(mappingStrategy, options),
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

            try
            {
                // Execute migration steps
                await ExecuteMigrationStepsAsync(execution, migrationPlan, scope);
                
                execution.Status = MigrationStatus.Completed;
                execution.CompletedAt = DateTime.UtcNow;
                execution.Progress = 100;
                
                _logger.LogInformation("Migration {ExecutionId} completed successfully", execution.Id);
            }
            catch (Exception ex)
            {
                execution.Status = MigrationStatus.Failed;
                execution.Error = ex.Message;
                execution.CompletedAt = DateTime.UtcNow;
                
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
                if (_snapshots.TryGetValue(snapshotId, out var snapshot))
                {
                    await RestoreFromSnapshotAsync(snapshot.ProcessInstanceId, snapshotId);
                }
            }

            execution.Status = MigrationStatus.RolledBack;
            execution.CompletedAt = DateTime.UtcNow;

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
            
            // Restore activity states
            foreach (var activityEntry in snapshot.ActivityStates)
            {
                var activityState = JsonSerializer.Deserialize<ActivityState>(activityEntry.Value);
                await RestoreActivityStateAsync(activityState!, restoreScope);
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
        // Simulate instance migration
        await Task.Delay(100);
    }

    private async Task ExecuteUpdateDefinitionsStep(MigrationExecution execution, MigrationStep step, IServiceScope scope)
    {
        // Simulate definition updates
        await Task.Delay(50);
    }

    private async Task ExecuteValidateResultsStep(MigrationExecution execution, MigrationStep step, IServiceScope scope)
    {
        // Simulate validation
        await Task.Delay(25);
    }

    // Mock data access methods
    private async Task<ProcessDefinitionInfo> GetProcessDefinitionAsync(string processKey, IServiceScope scope)
    {
        await Task.Delay(10);
        return new ProcessDefinitionInfo { Key = processKey, Name = $"Process {processKey}", Version = 1 };
    }

    private async Task<List<ProcessInstanceInfo>> GetActiveProcessInstancesAsync(string processKey, IServiceScope scope)
    {
        await Task.Delay(10);
        return new List<ProcessInstanceInfo>
        {
            new() { Id = Guid.NewGuid(), ProcessDefinitionKey = processKey, Status = "Active" },
            new() { Id = Guid.NewGuid(), ProcessDefinitionKey = processKey, Status = "Active" }
        };
    }

    private async Task<List<ActivityMappingRule>> CreateActivityMappingAsync(ProcessDefinitionInfo from, ProcessDefinitionInfo to)
    {
        await Task.Delay(10);
        return new List<ActivityMappingRule>
        {
            new() { FromActivityId = "start", ToActivityId = "start", MappingType = "Direct" },
            new() { FromActivityId = "task1", ToActivityId = "task1", MappingType = "Direct" },
            new() { FromActivityId = "end", ToActivityId = "end", MappingType = "Direct" }
        };
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

    private List<MigrationStep> GenerateMigrationSteps(List<ActivityMappingRule> mappings, MigrationOptions options)
    {
        return new List<MigrationStep>
        {
            new() { Name = "Create Snapshots", Type = "CreateSnapshots", Order = 1, Parameters = new Dictionary<string, object>() },
            new() { Name = "Migrate Instances", Type = "MigrateInstances", Order = 2, Parameters = new Dictionary<string, object>() },
            new() { Name = "Update Definitions", Type = "UpdateDefinitionsStep", Order = 3, Parameters = new Dictionary<string, object>() },
            new() { Name = "Validate Results", Type = "ValidateResults", Order = 4, Parameters = new Dictionary<string, object>() }
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

    // Mock implementation methods
    private async Task StoreMigrationPlanAsync(MigrationPlan plan, IServiceScope scope) => await Task.Delay(1);
    private async Task<MigrationPlan> GetMigrationPlanAsync(Guid planId, IServiceScope scope) => await Task.FromResult(new MigrationPlan { Id = planId });
    private async Task<MigrationExecution?> GetStoredMigrationExecutionAsync(Guid id, IServiceScope scope) => await Task.FromResult<MigrationExecution?>(null);
    private async Task StoreSnapshotAsync(LiveMigrationSnapshot snapshot, IServiceScope scope) => await Task.Delay(1);
    private async Task<LiveMigrationSnapshot?> GetStoredSnapshotAsync(Guid id, IServiceScope scope) => await Task.FromResult<LiveMigrationSnapshot?>(null);

    private List<ActivityInfo> ExtractActivities(ProcessDefinitionInfo process)
    {
        return new List<ActivityInfo>
        {
            new() { Id = "start", Name = "Start Event", Type = "StartEvent" },
            new() { Id = "task1", Name = "User Task", Type = "UserTask" },
            new() { Id = "end", Name = "End Event", Type = "EndEvent" }
        };
    }

    private List<VariableInfo> ExtractVariables(ProcessDefinitionInfo process)
    {
        return new List<VariableInfo>
        {
            new() { Name = "var1", Type = "string" },
            new() { Name = "var2", Type = "int" }
        };
    }

    private async Task<ProcessInstanceState> GetProcessInstanceAsync(Guid id, IServiceScope scope)
    {
        await Task.Delay(1);
        return new ProcessInstanceState { Id = id, Status = "Active" };
    }

    private async Task<List<TokenState>> GetActiveTokensAsync(Guid processInstanceId, IServiceScope scope)
    {
        await Task.Delay(1);
        return new List<TokenState> { new() { Id = Guid.NewGuid(), ActivityId = "task1" } };
    }

    private async Task<Dictionary<string, object>> GetVariablesAsync(Guid processInstanceId, IServiceScope scope)
    {
        await Task.Delay(1);
        return new Dictionary<string, object> { { "var1", "value1" } };
    }

    private async Task<Dictionary<string, string>> CaptureActivityStatesAsync(Guid processInstanceId, IServiceScope scope)
    {
        await Task.Delay(1);
        return new Dictionary<string, string> { { "activity1", JsonSerializer.Serialize(new ActivityState { Id = "activity1", Status = "Active" }) } };
    }

    private async Task RestoreProcessInstanceAsync(ProcessInstanceState state, IServiceScope scope) => await Task.Delay(1);
    private async Task RestoreTokenAsync(TokenState token, IServiceScope scope) => await Task.Delay(1);
    private async Task SetVariableAsync(Guid processInstanceId, string name, object value, IServiceScope scope) => await Task.Delay(1);
    private async Task RestoreActivityStateAsync(ActivityState state, IServiceScope scope) => await Task.Delay(1);
}

// Data Models
public class MigrationPlan
{
    public Guid Id { get; set; }
    public string FromProcessKey { get; set; } = string.Empty;
    public string ToProcessKey { get; set; } = string.Empty;
    public MigrationOptions Options { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public TimeSpan EstimatedDuration { get; set; }
    public string Complexity { get; set; } = string.Empty;
    public List<MigrationCompatibilityIssue> CompatibilityIssues { get; set; } = new();
    public List<ActivityMappingRule> ActivityMappings { get; set; } = new();
    public int AffectedInstances { get; set; }
    public List<MigrationStep> MigrationSteps { get; set; } = new();
    public string RiskAssessment { get; set; } = string.Empty;
    public RollbackPlan RollbackPlan { get; set; } = new();
}

public class MigrationOptions
{
    public bool CreateBackups { get; set; } = true;
    public bool ValidateBeforeMigration { get; set; } = true;
    public bool AllowPartialMigration { get; set; } = false;
    public TimeSpan Timeout { get; set; } = TimeSpan.FromHours(1);
    public int MaxConcurrentMigrations { get; set; } = 5;
}

public class MigrationExecution
{
    public Guid Id { get; set; }
    public Guid MigrationPlanId { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public MigrationStatus Status { get; set; }
    public bool IsDryRun { get; set; }
    public int Progress { get; set; }
    public string? Error { get; set; }
    public List<MigrationStepResult> Steps { get; set; } = new();
    public List<Guid> Snapshots { get; set; } = new();
}

public enum MigrationStatus
{
    NotFound,
    Planned,
    InProgress,
    Completed,
    Failed,
    RollingBack,
    RolledBack
}

public class MigrationCompatibilityIssue
{
    public string Type { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Recommendation { get; set; } = string.Empty;
    public string AffectedElement { get; set; } = string.Empty;
}

public class ActivityMappingRule
{
    public string FromActivityId { get; set; } = string.Empty;
    public string ToActivityId { get; set; } = string.Empty;
    public string MappingType { get; set; } = string.Empty; // Direct, Transform, Custom
}

public class MigrationStep
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public int Order { get; set; }
    public Dictionary<string, object> Parameters { get; set; } = new();
}

public class MigrationStepResult
{
    public string StepName { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Error { get; set; }
}

public class RollbackPlan
{
    public string Strategy { get; set; } = string.Empty;
    public TimeSpan EstimatedDuration { get; set; }
    public List<string> Steps { get; set; } = new();
}

public class LiveMigrationSnapshot
{
    public Guid Id { get; set; }
    public Guid ProcessInstanceId { get; set; }
    public DateTime CreatedAt { get; set; }
    public string ProcessState { get; set; } = string.Empty;
    public Dictionary<string, string> TokenStates { get; set; } = new();
    public Dictionary<string, string> Variables { get; set; } = new();
    public Dictionary<string, string> ActivityStates { get; set; } = new();
}

// Supporting classes
public class ProcessDefinitionInfo
{
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Version { get; set; }
}

public class ProcessInstanceInfo
{
    public Guid Id { get; set; }
    public string ProcessDefinitionKey { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

public class ActivityInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
}

public class VariableInfo
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
}

public class ProcessInstanceState
{
    public Guid Id { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class TokenState
{
    public Guid Id { get; set; }
    public string ActivityId { get; set; } = string.Empty;
}

public class ActivityState
{
    public string Id { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}
