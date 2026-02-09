namespace VertexBPMN.Domain.Model.Dmn.Core;

/// <summary>Import (6.3.3)</summary>
public sealed class Import : NamedElement
{
    public Uri ImportType { get; set; } = new("http://www.w3.org/2001/XMLSchema");
    public Uri? LocationURI { get; set; }
    public Uri Namespace { get; set; } = new("http://example.org/namespace");
}