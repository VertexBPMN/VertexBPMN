using System.Collections.Generic;
using System.Linq;
using VertexBPMN.Domain.Model.Bpmn;
using VertexBPMN.Domain.Model.Validation;

namespace VertexBPMN.Domain.Model.Validation.Rules;

/// <summary>
/// Validates terminate & cancel event presence and usage.
/// </summary>
internal sealed class TerminateAndCancelEventRule : IBpmnSemanticRule
{
    public IEnumerable<ValidationDiagnostic> Evaluate(BpmnModel model, SemanticValidationContext ctx)
    {
        var diagnostics = new List<ValidationDiagnostic>();

        foreach (var end in model.Events.OfType<EndEvent>())
        {
            if (end.Id is null) continue;
            var terminateDefs = end.EventDefinitions.OfType<TerminateEventDefinition>().Count();
            if (terminateDefs > 1)
                diagnostics.Add(Error("BPMN220", $"EndEvent '{end.Id}' has multiple terminate definitions", end.Id, "EndEvent"));
        }

        // CancelEventDefinition should appear only attached to boundary events on transactions
        foreach (var boundary in model.Events.OfType<BoundaryEvent>())
        {
            var cancelDefs = boundary.EventDefinitions.OfType<CancelEventDefinition>().ToList();
            if (cancelDefs.Count > 0 && boundary.AttachedToRef != null)
                diagnostics.Add(Error("BPMN221", $"Cancel boundary event '{boundary.Id}' not attached to a Transaction", boundary.Id, "Boundary"));
        }

        return diagnostics;
    }

    private static ValidationDiagnostic Error(string c, string m, string? id, string cat) => new(c, ValidationSeverity.Error, m, id, cat);
}
