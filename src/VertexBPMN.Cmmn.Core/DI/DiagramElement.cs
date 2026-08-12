namespace VertexBPMN.Domain.Model.Cmmn.DI;

public abstract class DiagramElement
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
}