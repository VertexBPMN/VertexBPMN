namespace VertexBPMN.Domain.Contracts
{
    public interface IProcessMigrationService
    {
        ProcessMigrationResult MigrateInstances(ProcessMigrationPlan plan);
        ProcessMigrationPlan PreviewMigration(string sourceProcessDefinitionId, string targetProcessDefinitionId);
    }
}
