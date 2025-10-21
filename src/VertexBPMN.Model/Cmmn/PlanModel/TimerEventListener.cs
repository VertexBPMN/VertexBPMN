namespace VertexBPMN.Domain.Model.Cmmn.PlanModel;

/// <summary>
/// Timer event listener (Figure 5.7, inherits from EventListener).
/// </summary>
public record TimerEventListener(
    Expression? TimerExpression = null,
    StartTrigger? TimerStart = null
) : EventListener();