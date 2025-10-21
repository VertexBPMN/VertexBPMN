namespace VertexBPMN.Domain.Model.Dmn.Enums;


#nullable enable

/// <summary>
/// Enum for hit policies in decision tables.
/// </summary>
public enum HitPolicy
{
    Unique,
    First,
    Priority,
    Any,
    Collect,
    RuleOrder,
    OutputOrder
}