using VertexBPMN.Domain.Model.Dmn.DRD;

namespace VertexBPMN.Domain.Model.Dmn.BusinessContext;

public sealed class OrganisationalUnit : BusinessContextElement
{
    public List<Decision> DecisionMade { get; } = new();
    public List<Decision> DecisionOwned { get; } = new();
}