using VertexBPMN.Core.Domain;
namespace VertexBPMN.Core.Services
{
    public interface IProcessMigrationService
    {
        ProcessMigrationResult MigrateInstances(ProcessMigrationPlan plan);
        ProcessMigrationPlan PreviewMigration(string sourceProcessDefinitionId, string targetProcessDefinitionId);
    }
}
