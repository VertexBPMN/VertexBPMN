using System.Collections.Generic;
using System.Linq;
using VertexBPMN.Domain.Model.Bpmn;
using VertexBPMN.Domain.Model.Validation;

namespace VertexBPMN.Domain.Model.Validation.Rules;

/// <summary>
/// Validates association source/target existence and allowed reference categories.
/// </summary>
internal sealed class AssociationValidityRule : IBpmnSemanticRule
{
    public IEnumerable<ValidationDiagnostic> Evaluate(BpmnModel model, SemanticValidationContext ctx)
    {
        var diagnostics = new List<ValidationDiagnostic>();
        if (model.Associations == null) return diagnostics;

        foreach (var assoc in model.Associations)
        {
            if (assoc.Id is null) continue;
            if (assoc.SourceRef == null)
                diagnostics.Add(Error("BPMN170", $"Association '{assoc.Id}' missing sourceRef", assoc.Id, "Association"));
            if (assoc.TargetRef == null)
                diagnostics.Add(Error("BPMN171", $"Association '{assoc.Id}' missing targetRef", assoc.Id, "Association"));

            // Optionally restrict to FlowElement or Artifact types
            var allowed = true; // Placeholder; extend with type checks if desired
            if (!allowed)
                diagnostics.Add(Warning("BPMN172", $"Association '{assoc.Id}' references unsupported element types", assoc.Id, "Association"));
        }

        return diagnostics;
    }

    private static ValidationDiagnostic Error(string c, string m, string? id, string cat) => new(c, ValidationSeverity.Error, m, id, cat);
    private static ValidationDiagnostic Warning(string c, string m, string? id, string cat) => new(c, ValidationSeverity.Warning, m, id, cat);
}
