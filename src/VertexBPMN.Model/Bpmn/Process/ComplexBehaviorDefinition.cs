using VertexBPMN.Domain.Model.Bpmn.Common;
using VertexBPMN.Domain.Model.Bpmn.Event;
using VertexBPMN.Domain.Model.Bpmn.Foundation;

namespace VertexBPMN.Domain.Model.Bpmn.Process;

#nullable enable

/// <summary>
/// Complex behavior definition, as per Figure 10.45.
/// </summary>
public record ComplexBehaviorDefinition(
    FormalExpression Condition,
    ImplicitThrowEvent Event
) : BaseElement();