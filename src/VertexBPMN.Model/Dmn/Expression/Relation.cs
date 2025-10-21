using VertexBPMN.Domain.Model.Dmn.Core;

namespace VertexBPMN.Domain.Model.Dmn.Expression;

/// <summary>
/// Relation (Figure 10-27, extends Expression). 
/// Notation: Boxed table-like with columns top, rows below. 
/// Semantics: List of contexts; equivalent to table without logic.
/// Examples: Credit history relation (Figure 10-28).
/// DMN 1.5: Jagged support; integrates with filters.
/// </summary>
public record Relation(
    List<List> Rows = null!, // [0..*] Rows as lists.
    List<InformationItem> Columns = null! // [0..*] Column definitions.
) : Core.Expression();