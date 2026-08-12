using System.Collections.ObjectModel;

namespace VertexBPMN.Domain.Model.Cmmn.DI;

public sealed class CmmnDi
{
    public Collection<CmmnStyle> Styles { get; } = new();
    public Collection<CmmnDiagram> Diagrams { get; } = new();
}