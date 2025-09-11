using System.Threading.Tasks;

namespace VertexBPMN.Domain.Contracts
{
    public interface ISimulationService
    {
        Task<SimulationResult> SimulateAsync(SimulationRequest request);
    }
}
