using VertexBPMN.Domain.Model.Dmn.Expression;

namespace VertexBPMN.Domain.Model.Dmn.Core;

/// <summary>
/// Item definition (Figure 7-7, extends NamedElement).
/// DMN 1.5: Deprecates AllowedValues; uses TypeConstraint; supports FunctionItem.
/// </summary>
public record ItemDefinition(
    string TypeRef,
    string? TypeLanguage = null,
    UnaryTests? AllowedValues = null, // Deprecated in 1.5.
    List<ItemDefinition> ItemComponents = null!,
    bool IsCollection = false,
    FunctionItem? FunctionItem = null,
    UnaryTests? TypeConstraint = null // [0..1] Replaces AllowedValues for constraints.
) : NamedElement();