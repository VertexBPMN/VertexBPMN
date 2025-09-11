using VertexBPMN.Domain;

namespace VertexBPMN.Core.Contracts
{
    public interface ISimulationService
    {
        Task<SimulationResult> SimulateAsync(SimulationRequest request);
    }
}
