using VertexBPMN.Domain.Entities;

namespace VertexBPMN.Studio.Services;

public interface IEngineAdministrationService
{
    Task<EngineConfiguration> GetEngineConfigurationAsync();
    Task UpdateEngineConfigurationAsync(EngineConfiguration configuration);
    Task<IEnumerable<EngineConnection>> GetEngineConnectionsAsync();
    Task AddEngineConnectionAsync(EngineConnection connection);
    Task UpdateEngineConnectionAsync(EngineConnection connection);
    Task RemoveEngineConnectionAsync(string connectionId);
}

public sealed class EngineAdministrationService : IEngineAdministrationService
{
    private readonly IBpmnEngineService _engineService;

    public EngineAdministrationService(IBpmnEngineService engineService) => _engineService = engineService;

    public Task<EngineConfiguration> GetEngineConfigurationAsync() => _engineService.GetEngineConfigurationAsync();
    public Task UpdateEngineConfigurationAsync(EngineConfiguration configuration) => _engineService.UpdateEngineConfigurationAsync(configuration);
    public Task<IEnumerable<EngineConnection>> GetEngineConnectionsAsync() => _engineService.GetEngineConnectionsAsync();
    public Task AddEngineConnectionAsync(EngineConnection connection) => _engineService.AddEngineConnectionAsync(connection);
    public Task UpdateEngineConnectionAsync(EngineConnection connection) => _engineService.UpdateEngineConnectionAsync(connection);
    public Task RemoveEngineConnectionAsync(string connectionId) => _engineService.RemoveEngineConnectionAsync(connectionId);
}
