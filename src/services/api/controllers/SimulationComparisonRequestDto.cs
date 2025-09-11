using VertexBPMN.Domain;

namespace VertexBPMN.Api.Controllers
{
    public class SimulationComparisonRequestDto
    {
        public SimulationResult? ResultA { get; set; }
        public SimulationResult? ResultB { get; set; }
    }
}
