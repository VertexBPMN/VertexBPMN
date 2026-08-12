using VertexBPMN.Domain.Model.Cmmn.Core;

namespace VertexBPMN.Domain.Model.Cmmn.DI;

public abstract class CmmnDiagramElement : DiagramElement
{
    public CmmnStyle? SharedStyle { get; set; }
    public CmmnStyle? LocalStyle { get; set; }
    public CmmnLabel? Label { get; set; }
    public CmmnElement? CmmnElementRef { get; set; }
}