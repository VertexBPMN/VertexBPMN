using System.Collections.Generic;
using System.Linq;
using VertexBPMN.Domain.Model.Bpmn;
using VertexBPMN.Domain.Model.Bpmn.Validation;

namespace VertexBPMN.Domain.Model.Bpmn.Validation.Rules;

internal sealed class DiagramConsistencyRule : IBpmnSemanticRule
{
    public IEnumerable<ValidationDiagnostic> Evaluate(BpmnModel model, SemanticValidationContext context)
    {
        var diagnostics = new List<ValidationDiagnostic>();

        if (model.Shapes != null)
        {
            var byElement = new Dictionary<string, List<BpmnShape>>();
            foreach (var shape in model.Shapes)
            {
                var refId = shape.BpmnElement?.Name;
                if (refId == null)
                {
                    diagnostics.Add(Error("BPMN100", $"Shape '{shape.Id}' without BPMN element reference", shape.Id, "DI"));
                    continue;
                }
                if (!byElement.TryGetValue(refId, out var list))
                    byElement[refId] = list = new List<BpmnShape>();
                list.Add(shape);
            }

            foreach (var kv in byElement.Where(kv => kv.Value.Count > 1))
                diagnostics.Add(new ValidationDiagnostic("BPMN101", ValidationSeverity.Warning,
                    $"Element '{kv.Key}' has {kv.Value.Count} shapes", kv.Key, "DI"));
        }

        if (model.Edges != null)
        {
            foreach (var edge in model.Edges)
            {
                if (edge.BpmnElement?.Name == null)
                    diagnostics.Add(Error("BPMN100", $"Edge '{edge.Id}' without BPMN element reference", edge.Id, "DI"));
                if (edge.Waypoint.Count < 2)
                    diagnostics.Add(new ValidationDiagnostic("BPMN102", ValidationSeverity.Warning,
                        $"Edge '{edge.Id}' has fewer than 2 waypoints", edge.Id, "DI"));
            }
        }

        return diagnostics;
    }

    private static ValidationDiagnostic Error(string c, string m, string? id, string cat)
        => new(c, ValidationSeverity.Error, m, id, cat);
}