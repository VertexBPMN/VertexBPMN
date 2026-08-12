using VertexBPMN.Domain.Entities;

namespace VertexBPMN.Domain.Interfaces
{
    public interface IProcessMigrationService
    {
        ProcessMigrationResult MigrateInstances(ProcessMigrationPlan plan);
        ProcessMigrationPlan PreviewMigration(string sourceProcessDefinitionId, string targetProcessDefinitionId);
    }
}
