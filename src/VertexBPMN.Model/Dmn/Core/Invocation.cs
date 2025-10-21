namespace VertexBPMN.Domain.Model.Dmn.Core;

/// <summary>
/// Invocation (extends Expression). 
/// Notation: Box with function name top; binding rows (param: expr). 
/// Semantics: Binds parameters; evaluates body. Equivalent to FEEL `f(p1: e1, ...)`.
/// Examples: `pmt(rate: 0.05/12, nper: 360, pv: 100000)` (Figure 10-4).
/// DMN 1.5: Supports external (Java/PMML); decision services; one binding per param.
/// </summary>
public record Invocation(
    string Name, // [1] Invoked function name.
    Expression CalledFunction, // [1] Yields function (e.g., LiteralExpression naming BKM).
    List<Binding> Bindings, // [0..*] One per formal param.
    bool IsExternal = false, // [0..1] For external functions.
    string? Text = null // [0..1] FEEL text for invocation.
) : Expression();