using System.Collections.Generic;
using System.Linq;
using VertexBPMN.Domain.Model.Bpmn;
using VertexBPMN.Domain.Model.Validation;

namespace VertexBPMN.Domain.Model.Validation.Rules;

/// <summary>
/// Validates interrupting start events: inside event subprocess isInterrupting must align; top-level non-interrupting not allowed for message/timer/signal.
/// </summary>
internal sealed class InterruptingStartEventContextRule : IBpmnSemanticRule
{
    public IEnumerable<ValidationDiagnostic> Evaluate(BpmnModel model, SemanticValidationContext ctx)
    {
        var diagnostics = new List<ValidationDiagnostic>();

        foreach (var se in model.Events.OfType<StartEvent>())
        {
            if (se.Id is null) continue;
            var parentSub = model.Subprocesses.FirstOrDefault(sp => se.Id.StartsWith(sp.Id + "_")); // heuristic naming
            var hasMessageTimerSignal = se.EventDefinitions.Any(ed => ed is MessageEventDefinition or TimerEventDefinition or SignalEventDefinition);

            if (parentSub == null && !se.IsInterrupting && hasMessageTimerSignal)
                diagnostics.Add(Error("BPMN210", $"Top-level StartEvent '{se.Id}' of type message/timer/signal must be interrupting", se.Id, "StartEvent"));

            if (parentSub != null && parentSub.TriggeredByEvent && se.IsInterrupting && hasMessageTimerSignal)
                diagnostics.Add(Warning("BPMN211", $"Event SubProcess StartEvent '{se.Id}' usually non-interrupting; confirm semantics", se.Id, "StartEvent"));
        }

        return diagnostics;
    }

        private static ValidationDiagnostic Error(string c, string m, string? id, string cat) => new(c, ValidationSeverity.Error, m, id, cat);
        private static ValidationDiagnostic Warning(string c, string m, string? id, string cat) => new(c, ValidationSeverity.Warning, m, id, cat);
}
