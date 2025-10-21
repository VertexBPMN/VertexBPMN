using System.Collections.Generic;
using VertexBPMN.Domain.Model.Bpmn;
using VertexBPMN.Domain.Model.Bpmn.Event;
using VertexBPMN.Domain.Model.Bpmn.Infrastructure;
using VertexBPMN.Domain.Model.Validation;
using VertexBPMN.Domain.Model.Xml.Validation;

namespace VertexBPMN.Domain.Model.Xml.Validation.Rules;

internal sealed class TimerEventExclusivityRule : IBpmnSemanticRule
{
    public IEnumerable<ValidationDiagnostic> Evaluate(BpmnModel model, SemanticValidationContext context)
    {
        foreach (var evt in model.Events)
        {
            foreach (var timer in evt.EventDefinitions.OfType<TimerEventDefinition>())
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