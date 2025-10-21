using System.Collections.Generic;
using VertexBPMN.Domain.Model.Bpmn;
using VertexBPMN.Domain.Model.Bpmn.Event;
using VertexBPMN.Domain.Model.Bpmn.Infrastructure;
using VertexBPMN.Domain.Model.Bpmn.Process;
using VertexBPMN.Domain.Model.Validation;
using VertexBPMN.Domain.Model.Xml.Validation;

namespace VertexBPMN.Domain.Model.Xml.Validation.Rules;

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
        foreach (var evt in model.Events)
        {
            foreach (var ed in evt.EventDefinitions)
            {
                if (ed is CompensationEventDefinition ced)
                {
                    if (ced.ActivityRef == null)
                        diagnostics.Add(Error("BPMN041", $"CompensationEventDefinition in '{evt.Id}' without ActivityRef", evt.Id, "Compensation"));
                    else if (ced.ActivityRef is Activity a && !a.IsForCompensation)
                        diagnostics.Add(Error("BPMN041", $"Activity '{a.Id}' referenced by compensation is not marked IsForCompensation", a.Id, "Compensation"));
                }
            }
        }

        return diagnostics;
    }

    private static ValidationDiagnostic Error(string c, string m, string? id, string cat)
        => new(c, ValidationSeverity.Error, m, id, cat);
}