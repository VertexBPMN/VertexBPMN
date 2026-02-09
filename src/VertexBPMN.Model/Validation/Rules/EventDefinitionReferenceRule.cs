using System.Collections.Generic;
using System.Linq;
using VertexBPMN.Domain.Model.Bpmn;
using VertexBPMN.Domain.Model.Validation;

namespace VertexBPMN.Domain.Model.Validation.Rules;

/// <summary>
/// Validates completeness of event definitions & formal expressions.
/// (Complement to DefinitionUniquenessRule which already checks basic references.)
/// </summary>
internal sealed class EventDefinitionReferenceRule : IBpmnSemanticRule
{
    public IEnumerable<ValidationDiagnostic> Evaluate(BpmnModel model, SemanticValidationContext ctx)
    {
        var diagnostics = new List<ValidationDiagnostic>();

        foreach (var evt in model.Events.OfType<CatchEvent>())
        {
            if (evt.Id is null) continue;
            foreach (var ed in evt.EventDefinitions)
            {
                switch (ed)
                {
                    case ConditionalEventDefinition ced when ced.Condition?.Text.ToString() is string b && string.IsNullOrWhiteSpace(b):
                        diagnostics.Add(Error("BPMN180", $"ConditionalEventDefinition in '{evt.Id}' has empty condition body", evt.Id, "EventDef"));
                        break;
                    case LinkEventDefinition led when string.IsNullOrWhiteSpace(led.Name):
                        diagnostics.Add(Warning("BPMN181", $"LinkEventDefinition in '{evt.Id}' missing name", evt.Id, "EventDef"));
                        break;
                }
            }
        }

        return diagnostics;
    }

    private static ValidationDiagnostic Error(string c, string m, string? id, string cat) => new(c, ValidationSeverity.Error, m, id, cat);
    private static ValidationDiagnostic Warning(string c, string m, string? id, string cat) => new(c, ValidationSeverity.Warning, m, id, cat);
}
