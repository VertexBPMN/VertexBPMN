using System.Collections.Generic;
using VertexBPMN.Domain.Model.Bpmn;
using VertexBPMN.Domain.Model.Bpmn.Validation;

namespace VertexBPMN.Domain.Model.Bpmn.Validation.Rules;

internal sealed class TimerEventExclusivityRule : IBpmnSemanticRule
{
    public IEnumerable<ValidationDiagnostic> Evaluate(BpmnModel model, SemanticValidationContext context)
    {
        foreach (CatchEvent evt in model.Events)
        {
            foreach (var timer in evt.EventDefinition.OfType<TimerEventDefinition>())
            {
                int present = 0;
                if (timer.TimeDate != null) present++;
                if (timer.TimeDuration != null) present++;
                if (timer.TimeCycle != null) present++;
                if (present > 1)
                    yield return new ValidationDiagnostic("BPMN044", ValidationSeverity.Error,
                        $"TimerEventDefinition in '{evt.Id}' has multiple time specs", evt.Id, "EventTime");
            }
        }
    }
}