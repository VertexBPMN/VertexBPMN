namespace VertexBPMN.Domain.Model.Dmn;

public record DmnOutput(string Id, string Label, string TypeRef)
{
    // Backward compatibility
    public DmnOutput(string Id, string Label) : this(Id, Label, "string") { }
}