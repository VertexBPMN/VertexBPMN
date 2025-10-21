using System.Collections.Generic;
using System.Linq;
using VertexBPMN.Domain.Model.Bpmn;
using VertexBPMN.Domain.Model.Bpmn.Common;
using VertexBPMN.Domain.Model.Bpmn.Event;
using VertexBPMN.Domain.Model.Bpmn.Infrastructure;
using VertexBPMN.Domain.Model.Validation;
using VertexBPMN.Domain.Model.Xml.Validation;

namespace VertexBPMN.Domain.Model.Xml.Validation.Rules;

/// <summary>
/// Ensures formal expressions (conditions, timer values) are not empty when present.
/// </summary>
internal sealed class FormalExpressionNonEmptyRule : IBpmnSemanticRule
{
    public IEnumerable<ValidationDiagnostic> Evaluate(BpmnModel model, SemanticValidationContext ctx)
    {
        var diagnostics = new List<ValidationDiagnostic>();

        foreach (var sf in model.SequenceFlows)
        {
            var body = sf.ConditionExpression?.Body;
            if (sf.ConditionExpression != null && string.IsNullOrWhiteSpace(body))
                diagnostics.Add(Error("BPMN230", $"SequenceFlow '{sf.Id}' has empty condition expression", sf.Id, "Expression"));
        }

        foreach (var evt in model.Events)
        {
            foreach (var td in evt.EventDefinitions.OfType<TimerEventDefinition>())
            {
                if (td.TimeDate != null && string.IsNullOrWhiteSpace(td.TimeDate.Body))
                    diagnostics.Add(Error("BPMN231", $"TimerEventDefinition in '{evt.Id}' empty timeDate body", evt.Id, "Expression"));
                if (td.TimeDuration != null && string.IsNullOrWhiteSpace(td.TimeDuration.Body))
                    diagnostics.Add(Error("BPMN232", $"TimerEventDefinition in '{evt.Id}' empty timeDuration body", evt.Id, "Expression"));
                if (td.TimeCycle != null && string.IsNullOrWhiteSpace(td.TimeCycle.Body))
                    diagnostics.Add(Error("BPMN233", $"TimerEventDefinition in '{evt.Id}' empty timeCycle body", evt.Id, "Expression"));
            }
        }

        return diagnostics;
    }

    private static ValidationDiagnostic Error(string c, string m, string? id, string cat) => new(c, ValidationSeverity.Error, m, id, cat);
}
