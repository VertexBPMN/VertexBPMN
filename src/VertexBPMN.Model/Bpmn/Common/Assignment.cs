using VertexBPMN.Domain.Model.Bpmn.Foundation;

namespace VertexBPMN.Domain.Model.Bpmn.Common;

/// <summary>
/// Assignment stub used in DataAssociation.
/// </summary>
public record Assignment(FormalExpression From, FormalExpression To) : BaseElement;