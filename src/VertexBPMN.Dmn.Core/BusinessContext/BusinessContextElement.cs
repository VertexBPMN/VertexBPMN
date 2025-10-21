using VertexBPMN.Domain.Model.Dmn.Core;

namespace VertexBPMN.Domain.Model.Dmn.BusinessContext;

public abstract class BusinessContextElement : NamedElement
{
    public Uri? URI { get; set; }
}