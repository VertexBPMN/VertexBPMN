using System.Collections.Generic;
using System.Linq;
using VertexBPMN.Domain.Model.Bpmn;
using VertexBPMN.Domain.Model.Bpmn.Validation;

namespace VertexBPMN.Domain.Model.Bpmn.Validation.Rules;

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
            var body = sf.ConditionExpression?.Text;
            if (sf.ConditionExpression != null && (body == null || body.All(string.IsNullOrWhiteSpace)))
                diagnostics.Add(Error("BPMN230", $"SequenceFlow '{sf.Id}' has empty condition expression", sf.Id, "Expression"));
        }

        foreach (CatchEvent evt in model.Events)
        {
            foreach (var td in evt.EventDefinition.OfType<TimerEventDefinition>())
            {
                if (td.TimeDate != null && td.TimeDate.Text.All(string.IsNullOrWhiteSpace))
                    diagnostics.Add(Error("BPMN231", $"TimerEventDefinition in '{evt.Id}' empty timeDate body", evt.Id, "Expression"));
                if (td.TimeDuration != null && td.TimeDuration.Text.All(string.IsNullOrWhiteSpace))
                    diagnostics.Add(Error("BPMN232", $"TimerEventDefinition in '{evt.Id}' empty timeDuration body", evt.Id, "Expression"));
                if (td.TimeCycle != null && td.TimeCycle.Text.All(string.IsNullOrWhiteSpace))
                    diagnostics.Add(Error("BPMN233", $"TimerEventDefinition in '{evt.Id}' empty timeCycle body", evt.Id, "Expression"));
            }
        }

        return diagnostics;
    }

    private static ValidationDiagnostic Error(string c, string m, string? id, string cat) => new(c, ValidationSeverity.Error, m, id, cat);
}
