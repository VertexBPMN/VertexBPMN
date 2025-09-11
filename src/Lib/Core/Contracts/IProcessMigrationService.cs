using VertexBPMN.Domain;

namespace VertexBPMN.Core.Contracts
{
    public interface IProcessMigrationService
    {
        ProcessMigrationResult MigrateInstances(ProcessMigrationPlan plan);
        ProcessMigrationPlan PreviewMigration(string sourceProcessDefinitionId, string targetProcessDefinitionId);
    }
}
