using VertexBPMN.Domain.Entities;

namespace VertexBPMN.Studio.Services;

internal interface IEngineAdministrationService
{
    Task<EngineConfiguration> GetEngineConfigurationAsync();
    Task UpdateEngineConfigurationAsync(EngineConfiguration configuration);
    Task<IEnumerable<EngineConnection>> GetEngineConnectionsAsync();
    Task AddEngineConnectionAsync(EngineConnection connection);
    Task UpdateEngineConnectionAsync(EngineConnection connection);
    Task RemoveEngineConnectionAsync(string connectionId);
}
