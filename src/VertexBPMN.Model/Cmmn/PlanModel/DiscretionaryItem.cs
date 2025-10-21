using VertexBPMN.Domain.Model.Cmmn.CaseModel;

namespace VertexBPMN.Domain.Model.Cmmn.PlanModel;

/// <summary>
/// Discretionary item (5.4.9.1.1, inherits from TableItem).
/// </summary>
public record DiscretionaryItem(
    PlanItemDefinition DefinitionRef,
    ItemControl? ItemControl = null,
    Role? AuthorizerRef = null
) : TableItem();