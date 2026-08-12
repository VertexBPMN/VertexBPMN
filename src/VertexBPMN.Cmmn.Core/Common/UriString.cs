namespace VertexBPMN.Domain.Model.Cmmn.Common;

public readonly record struct UriString(string Value)
{
    public override string ToString() => Value;
}