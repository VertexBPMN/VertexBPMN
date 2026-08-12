namespace VertexBPMN.Domain.Model.Cmmn.Core;

public sealed class Extension : CmmnElement
{
    public ExtensionDefinition Definition { get; set; } = new();
}