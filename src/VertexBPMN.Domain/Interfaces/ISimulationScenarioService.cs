using VertexBPMN.Domain.Entities;

namespace VertexBPMN.Domain.Interfaces
{
    public interface ISimulationScenarioService
    {
        Task<IEnumerable<SimulationScenario>> GetAllAsync(string tenantId = null);
        Task<SimulationScenario?> GetByIdAsync(string id);
        Task<SimulationScenario> CreateAsync(SimulationScenario scenario);
        Task<SimulationScenario?> UpdateAsync(string id, SimulationScenario scenario);
        Task<bool> DeleteAsync(string id);
    }
}
