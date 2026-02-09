using System.Collections.Generic;
using System.Linq;
using VertexBPMN.Domain.Model.Bpmn;
using VertexBPMN.Domain.Model.Validation;

namespace VertexBPMN.Domain.Model.Validation.Rules;

/// <summary>
/// Validates subprocess semantics: multi-instance markers, triggeredByEvent usage, transaction basics.
/// </summary>
internal sealed class SubProcessMultiplicityAndTriggerRule : IBpmnSemanticRule
{
    public IEnumerable<ValidationDiagnostic> Evaluate(BpmnModel model, SemanticValidationContext ctx)
    {
        var diagnostics = new List<ValidationDiagnostic>();

        foreach (var sp in model.Subprocesses)
        {
            if (sp.Id is null) continue;
            // Multi-instance heuristic: warn if IsMultiInstance set but no internal elements >1
            if (sp.LoopCharacteristics is MultiInstanceLoopCharacteristics  && (sp.FlowElements == null || sp.FlowElements.Count == 0))
                diagnostics.Add(Warning("BPMN131", $"SubProcess '{sp.Id}' marked multi-instance but contains no flow elements", sp.Id, "SubProcess"));

            // TriggeredByEvent subprocess should generally be event subprocess (no incoming/outgoing sequence flows except internal)
            List<SequenceFlow> incoming;
            if (!ctx.Incoming.TryGetValue(sp.Id, out incoming)) incoming = new List<SequenceFlow>();
            if (sp.TriggeredByEvent && incoming.Count > 0)
                diagnostics.Add(Error("BPMN132", $"Event SubProcess '{sp.Id}' has incoming SequenceFlows", sp.Id, "SubProcess"));

            // Transaction: simplistic check for boundary cancel / compensation events
            if (sp is Transaction t)
            {
                var boundaries = model.Events.OfType<BoundaryEvent>().Where(b => b.AttachedToRef?.Name == t.Id).ToList();
                var hasCancel = boundaries.Any(b => b.EventDefinitions.OfType<CancelEventDefinition>().Any());
                if (!hasCancel)
                    diagnostics.Add(Warning("BPMN133", $"Transaction SubProcess '{t.Id}' without cancel boundary event", t.Id, "SubProcess"));
            }
        }

        return diagnostics;
    }

    private static ValidationDiagnostic Error(string c, string m, string? id, string cat) => new(c, ValidationSeverity.Error, m, id, cat);
    private static ValidationDiagnostic Warning(string c, string m, string? id, string cat) => new(c, ValidationSeverity.Warning, m, id, cat);
}
