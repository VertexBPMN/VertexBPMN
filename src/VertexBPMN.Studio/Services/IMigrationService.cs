using System.Text.Json;

namespace VertexBPMN.Studio.Services;

public interface IMigrationService
{
    Task<JsonElement> PreviewAsync(string sourceProcessDefinitionId, string targetProcessDefinitionId, CancellationToken cancellationToken = default);
    Task<JsonElement> ExecuteAsync(JsonElement plan, CancellationToken cancellationToken = default);
    Task<JsonElement> GetStatusAsync(string migrationId, CancellationToken cancellationToken = default);
    Task<JsonElement> CreateSnapshotAsync(string processInstanceId, CancellationToken cancellationToken = default);
    Task<JsonElement> RestoreFromSnapshotAsync(string processInstanceId, string snapshotId, CancellationToken cancellationToken = default);
    Task<JsonElement> RollbackAsync(string migrationId, CancellationToken cancellationToken = default);
}
