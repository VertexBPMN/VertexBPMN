using System.Collections.Generic;
using System.Linq;
using VertexBPMN.Domain.Model.Bpmn;
using VertexBPMN.Domain.Model.Validation;

namespace VertexBPMN.Domain.Model.Validation.Rules;

/// <summary>
/// Ensures uniqueness of message name, signal name, error code, escalation code across the model (Error duplicates already warned by DefinitionUniquenessRule but we add stricter errors for collisions causing ambiguity in event routing).
/// </summary>
internal sealed class UniqueMessageSignalErrorEscalationRule : IBpmnSemanticRule
{
    public IEnumerable<ValidationDiagnostic> Evaluate(BpmnModel model, SemanticValidationContext ctx)
    {
        var diagnostics = new List<ValidationDiagnostic>();

        CheckStrict(model.Messages.Select(m => m.Name), "Message", "BPMN200", diagnostics);
        CheckStrict(model.Signals.Select(s => s.Name), "Signal", "BPMN201", diagnostics);
        CheckStrict(model.Errors.Select(e => e.ErrorCode), "ErrorCode", "BPMN202", diagnostics);
        CheckStrict(model.Escalations.Select(e => e.EscalationCode), "EscalationCode", "BPMN203", diagnostics);

        return diagnostics;
    }

    private static void CheckStrict(IEnumerable<string?> values, string label, string code, List<ValidationDiagnostic> sink)
    {
        var groups = values.Where(v => !string.IsNullOrWhiteSpace(v)).GroupBy(v => v!).Where(g => g.Count() > 1);
        foreach (var g in groups)
            sink.Add(new ValidationDiagnostic(code, ValidationSeverity.Error, $"Duplicate {label} '{g.Key}' -> must be unique", null, label));
    }
}
