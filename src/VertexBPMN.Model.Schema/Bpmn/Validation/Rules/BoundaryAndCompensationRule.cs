using System.Collections.Generic;
using VertexBPMN.Domain.Model.Bpmn;
using VertexBPMN.Domain.Model.Bpmn.Validation;

namespace VertexBPMN.Domain.Model.Bpmn.Validation.Rules;

internal sealed class BoundaryAndCompensationRule : IBpmnSemanticRule
{
    public IEnumerable<ValidationDiagnostic> Evaluate(BpmnModel model, SemanticValidationContext context)
    {
        var diagnostics = new List<ValidationDiagnostic>();

        // BPMN040 boundary events attachment
        foreach (var boundary in model.Events.OfType<BoundaryEvent>())
        {
            if (boundary.AttachedToRef == null)
                diagnostics.Add(Error("BPMN040", $"BoundaryEvent '{boundary.Id}' missing attachedToRef", boundary.Id, "Boundary"));
        }

        // BPMN041 compensation
        foreach (BoundaryEvent evt in model.Events)
        {
            foreach (var ed in evt.EventDefinition)
            {
                if (ed is CompensateEventDefinition ced)
                {
                    if (ced.ActivityRef == null)
                    {
                        diagnostics.Add(Error("BPMN041", $"CompensationEventDefinition in '{evt.Id}' without ActivityRef", evt.Id, "Compensation"));
                    }
                    else
                    {
                        // Find the referenced Activity by its XmlQualifiedName
                        var activity = model.Activities?.FirstOrDefault(a => a.Name == ced.ActivityRef.Name);
                        if (activity != null && !activity.IsForCompensation)
                        {
                            diagnostics.Add(Error("BPMN041", $"Activity '{activity.Id}' referenced by compensation is not marked IsForCompensation", activity.Id, "Compensation"));
                        }
                    }
                }
            }
        }

        return diagnostics;
    }

    private static ValidationDiagnostic Error(string c, string m, string? id, string cat)
        => new(c, ValidationSeverity.Error, m, id, cat);
}