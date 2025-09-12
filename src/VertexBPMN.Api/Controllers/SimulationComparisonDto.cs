using System.Collections.Generic;
namespace VertexBPMN.Api.Controllers
{
    public class SimulationComparisonDto
    {
        public int StepCountDifference { get; set; }
        public Dictionary<string, (object? A, object? B)>? VariableDifferences { get; set; }
        public bool CompletedDifference { get; set; }
    }
}
