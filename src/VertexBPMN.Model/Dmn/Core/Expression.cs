namespace VertexBPMN.Domain.Model.Dmn.Core;

/// <summary>
/// Abstract superclass for expressions (Figure 7-6, Figure 10-27). 
/// Notation: Rectangular box; supports nesting. Semantics: FEEL(e, s) with lexical scoping.
/// DMN 1.5: Supports ternary logic, implicit conversions, recursion.
/// </summary>
public abstract record Expression(
    string? TypeRef = null,
    string? ExpressionLanguage = null // [0..1] URI (default FEEL); overrides Definitions.
) : DMNElement();