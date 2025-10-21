namespace VertexBPMN.Domain.Model.Dmn.Enums;

/// <summary>
/// Enum for built-in aggregators in decision tables (DMN 1.5, Table 43).
/// </summary>
public enum BuiltinAggregator
{
    Sum,
    Min,
    Max,
    Count,
    Average // DMN 1.5 note: Average not standard but derived via sum/count; included for convenience.
}