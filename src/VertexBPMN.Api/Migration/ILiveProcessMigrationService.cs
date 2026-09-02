namespace VertexBPMN.Api.Migration;

/// <summary>
/// Live Process Migration System
/// Olympic-level feature: Innovation Differentiators - Live Process Migration
/// </summary>
public interface ILiveProcessMigrationService
{
    Task<MigrationPlan> CreateMigrationPlanAsync(string fromProcessKey, string toProcessKey, MigrationOptions options, string? tenantId = null);
    Task<MigrationPlan> CreateMigrationPlanByDefinitionIdAsync(Guid fromProcessDefinitionId, Guid toProcessDefinitionId, MigrationOptions options, string? tenantId = null);
    Task<MigrationExecution> ExecuteMigrationAsync(Guid migrationPlanId, bool dryRun = false, string? tenantId = null);
    Task<MigrationStatus> GetMigrationStatusAsync(Guid migrationId, string? tenantId = null);
    Task<bool> RollbackMigrationAsync(Guid migrationId, string? tenantId = null);
    Task<List<MigrationCompatibilityIssue>> ValidateCompatibilityAsync(string fromProcessKey, string toProcessKey, string? tenantId = null);
    Task<LiveMigrationSnapshot> CreateSnapshotAsync(Guid processInstanceId, string? tenantId = null);
    Task<bool> RestoreFromSnapshotAsync(Guid processInstanceId, Guid snapshotId, string? tenantId = null);
}
