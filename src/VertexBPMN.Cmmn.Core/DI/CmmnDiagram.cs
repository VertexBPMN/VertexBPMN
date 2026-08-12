using System.Collections.ObjectModel;
using VertexBPMN.Domain.Model.Cmmn.Core;

namespace VertexBPMN.Domain.Model.Cmmn.DI;

public sealed class CmmnDiagram
{
    public string? Name { get; set; }
    public string? Documentation { get; set; }
    public double Resolution { get; set; } = 300d;
    public CmmnStyle? SharedStyle { get; set; }
    public CmmnStyle? LocalStyle { get; set; }
    public Collection<CmmnDiagramElement> DiagramElements { get; } = new();
    public (double Width, double Height)? Size { get; set; }
    public CmmnElement? CmmnElementRef { get; set; }
}