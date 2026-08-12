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