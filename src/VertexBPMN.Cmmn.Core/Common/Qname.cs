namespace VertexBPMN.Domain.Model.Cmmn.Common;

public readonly record struct Qname(string? NamespaceUri, string LocalName)
{
    public override string ToString() => string.IsNullOrWhiteSpace(NamespaceUri) ? LocalName : $"{{{NamespaceUri}}}{LocalName}";
}