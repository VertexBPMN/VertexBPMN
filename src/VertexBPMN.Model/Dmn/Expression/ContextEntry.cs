using VertexBPMN.Domain.Model.Dmn.Core;

namespace VertexBPMN.Domain.Model.Dmn.Expression;

/// <summary>
/// ContextEntry. DMN 1.5: Supports "result" for final value.
/// </summary>
public record ContextEntry(
    InformationItem? Variable = null, // [0..1] Defines local variable.
    Core.Expression Value = null // [1] Boxed value expression.
) : DMNElement();