using System.Collections.Generic;
using System.Linq;
using VertexBPMN.Domain.Model.Bpmn;
using VertexBPMN.Domain.Model.Bpmn.Validation;

namespace VertexBPMN.Domain.Model.Bpmn.Validation.Rules;

internal sealed class DefinitionUniquenessRule : IBpmnSemanticRule
{
    public IEnumerable<ValidationDiagnostic> Evaluate(BpmnModel model, SemanticValidationContext context)
    {
        var diagnostics = new List<ValidationDiagnostic>();
        Check(model.Messages.Select(m => (m.Id, m.Name)), "Message", diagnostics);
        Check(model.Signals.Select(s => (s.Id, s.Name)), "Signal", diagnostics);
        Check(model.Errors.Select(e => (e.Id, e.ErrorCode)), "ErrorCode", diagnostics);
        Check(model.Escalations.Select(e => (e.Id, e.EscalationCode)), "EscalationCode", diagnostics);

        // BPMN071 event definition references
        foreach (CatchEvent evt in model.Events)
        {
            foreach (var def in evt.EventDefinition)
            {
                switch (def)
                {
                    case MessageEventDefinition med when med.MessageRef == null:
                        diagnostics.Add(Error("BPMN071", $"MessageEventDefinition in '{evt.Id}' missing MessageRef", evt.Id, "EventRef"));
                        break;
                    case SignalEventDefinition sed when sed.SignalRef == null:
                        diagnostics.Add(Error("BPMN071", $"SignalEventDefinition in '{evt.Id}' missing SignalRef", evt.Id, "EventRef"));
                        break;
                    case ErrorEventDefinition eed when eed.ErrorRef == null:
                        diagnostics.Add(Error("BPMN071", $"ErrorEventDefinition in '{evt.Id}' missing ErrorRef", evt.Id, "EventRef"));
                        break;
                    case EscalationEventDefinition esd when esd.EscalationRef == null:
                        diagnostics.Add(Error("BPMN071", $"EscalationEventDefinition in '{evt.Id}' missing EscalationRef", evt.Id, "EventRef"));
                        break;
                }
            }
        }

        return diagnostics;
    }

    private static void Check(IEnumerable<(string? Id, string? Key)> items, string label, List<ValidationDiagnostic> sink)
    {
        var seen = new Dictionary<string, List<string?>>();
        foreach (var (id, key) in items)
        {
            if (string.IsNullOrWhiteSpace(key)) continue;
            if (!seen.TryGetValue(key!, out var list))
                seen[key!] = list = new List<string?>();
            list.Add(id);
        }

        foreach (var kv in seen.Where(kv => kv.Value.Count > 1))
        {
            sink.Add(new ValidationDiagnostic("BPMN070", ValidationSeverity.Warning,
                $"{label} '{kv.Key}' defined multiple times (Ids: {string.Join(", ", kv.Value.Where(v=>v!=null))}) -> Fix: choose unique {label}",
                kv.Value.FirstOrDefault(), label));
        }
    }

    private static ValidationDiagnostic Error(string c, string m, string? id, string cat)
        => new(c, ValidationSeverity.Error, m, id, cat);
}