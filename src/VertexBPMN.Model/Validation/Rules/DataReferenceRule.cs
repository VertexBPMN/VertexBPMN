using System.Collections.Generic;
using VertexBPMN.Domain.Model.Bpmn;
using VertexBPMN.Domain.Model.Validation;

namespace VertexBPMN.Domain.Model.Validation.Rules;

internal sealed class DataReferenceRule : IBpmnSemanticRule
{
    public IEnumerable<ValidationDiagnostic> Evaluate(BpmnModel model, SemanticValidationContext context)
    {
        var diagnostics = new List<ValidationDiagnostic>();

        // BPMN060 / 061
        foreach (var dref in model.DataObjectReferences)
            if (dref.DataObjectRef == null)
                diagnostics.Add(Error("BPMN060", $"DataObjectReference '{dref.Id}' missing DataObjectRef", dref.Id, "Data"));
        foreach (var dsref in model.DataStoreReferences)
            if (dsref.DataStoreRef == null)
                diagnostics.Add(Error("BPMN061", $"DataStoreReference '{dsref.Id}' missing DataStoreRef", dsref.Id, "Data"));

        // BPMN062 property uniqueness
        var names = new HashSet<string>();
        foreach (var p in model.Properties)
        {
            var name = p.Id ?? p.GetType().Name;
            if (!names.Add(name))
                diagnostics.Add(new ValidationDiagnostic("BPMN062", ValidationSeverity.Warning,
                    $"Duplicate property key '{name}'", p.Id, "Data"));
        }

        // BPMN063 IO spec emptiness
        foreach (var act in model.ActivityIo)
        {
            if (act != null &&
                act.DataInputs.Count == 0 &&
                act.DataOutputs.Count == 0)
            {
                diagnostics.Add(new ValidationDiagnostic("BPMN063", ValidationSeverity.Warning,
                    $"Activity '{act.Id}' IO-Spec without inputs/outputs", act.Id, "Data"));
            }
        }

        return diagnostics;
    }

    private static ValidationDiagnostic Error(string c, string m, string? id, string cat)
        => new(c, ValidationSeverity.Error, m, id, cat);
}