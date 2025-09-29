namespace VertexBPMN.Domain.Model.Dmn;

public record DmnInput(string Id, string Label, string TypeRef)
{
    // Backward compatibility (previous tests used ctor with 2 args)
    public DmnInput(string Id, string Label) : this(Id, Label, "string") { }
}