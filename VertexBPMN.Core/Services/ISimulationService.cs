using System.Threading.Tasks;
using VertexBPMN.Core.Domain;

namespace VertexBPMN.Core.Services
{
    public interface ISimulationService
    {
        Task<SimulationResult> SimulateAsync(SimulationRequest request);
    }
}
