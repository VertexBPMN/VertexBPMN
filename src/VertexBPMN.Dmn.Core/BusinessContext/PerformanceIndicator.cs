using VertexBPMN.Domain.Model.Dmn.DRD;

namespace VertexBPMN.Domain.Model.Dmn.BusinessContext;

public sealed class PerformanceIndicator : BusinessContextElement
{
    public List<Decision> ImpactingDecision { get; } = new();
}