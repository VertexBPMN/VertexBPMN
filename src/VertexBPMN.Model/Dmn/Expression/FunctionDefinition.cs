using VertexBPMN.Domain.Model.Dmn.Core;
using VertexBPMN.Domain.Model.Dmn.Enums;

namespace VertexBPMN.Domain.Model.Dmn.Expression;

/// <summary>
/// FunctionDefinition (Figure 10-27, extends Expression). 
/// Notation: Box with parameters top; body below. 
/// Semantics: Deterministic, side-effect-free; binds formal params.
/// Examples: `function(p, r, n) p * r / (1 - power(1+r, -n))` (Figure 11-45).
/// DMN 1.5: External (Java/PMML); type mappings (Table 47).
/// </summary>
public record FunctionDefinition(
    List<InformationItem> FormalParameters = null!, // [0..*] Params with typeRef.
    Core.Expression? Body = null, // [0..1] Boxed body.
    FunctionKind Kind = FunctionKind.Feel // [1] FEEL/Java/PMML.
) : Core.Expression();