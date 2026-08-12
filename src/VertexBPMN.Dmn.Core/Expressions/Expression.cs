using VertexBPMN.Domain.Model.Dmn.Core;

namespace VertexBPMN.Domain.Model.Dmn.Expressions;

public abstract class Expression : DMNElement
{
    public string? TypeRef { get; set; }
}