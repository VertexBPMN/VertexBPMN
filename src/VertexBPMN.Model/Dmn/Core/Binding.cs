namespace VertexBPMN.Domain.Model.Dmn.Core;

/// <summary>
/// Binding for invocation.
/// </summary>
public record Binding(
    InformationItem Parameter,
    Expression? BindingFormula = null
) : DMNElement();