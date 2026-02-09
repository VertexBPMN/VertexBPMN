using VertexBPMN.Domain.Entities;

namespace VertexBPMN.Domain.Interfaces
{
    public interface ISimulationService
    {
        Task<SimulationResult> SimulateAsync(SimulationRequest request);
    }
}
