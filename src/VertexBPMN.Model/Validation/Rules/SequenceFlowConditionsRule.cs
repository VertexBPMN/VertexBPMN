using System.Collections.Generic;
using System.Linq;
using VertexBPMN.Domain.Model.Bpmn;
using VertexBPMN.Domain.Model.Validation;

namespace VertexBPMN.Domain.Model.Validation.Rules;

internal sealed class SequenceFlowConditionsRule : IBpmnSemanticRule
{
    public IEnumerable<ValidationDiagnostic> Evaluate(BpmnModel model, SemanticValidationContext ctx)
    {
        var diagnostics = new List<ValidationDiagnostic>();

        // BPMN020 / 021 default flow correctness
        foreach (var gw in model.Gateways)
        {
            if (gw.Id is null || gw.Id is null) continue;
            var defaultId = gw.Id;
            var isOutgoing = ctx.Outgoing.TryGetValue(gw.Id, out var outs) && outs.Any(sf => sf.Id == defaultId);
            if (!isOutgoing)
                diagnostics.Add(Error("BPMN020", $"Gateway '{gw.Id}' declares default flow '{defaultId}' which is not outgoing", gw.Id, "Gateway"));

            var df = model.SequenceFlows.FirstOrDefault(sf => sf.Id == defaultId);
            if (df?.ConditionExpression != null)
                diagnostics.Add(Error("BPMN021", $"Default SequenceFlow '{defaultId}' has a condition", df.Id, "Gateway"));
        }

        // BPMN022 condition only allowed from activity or certain gateways
        foreach (var sf in model.SequenceFlows)
        {
            if (sf.ConditionExpression == null) continue;
            var source = sf.SourceRef;
            var allowed = source is ExclusiveGateway
                          || source is InclusiveGateway
                          || source is ComplexGateway
                          || source is Activity;
            if (!allowed)
                diagnostics.Add(Error("BPMN022",
                    $"SequenceFlow '{sf.Id}' has condition from disallowed source '{source?.GetType().Name}'",
                    sf.Id, "SequenceFlow"));
        }

        return diagnostics;
    }

    private static ValidationDiagnostic Error(string c, string m, string? id, string cat)
        => new(c, ValidationSeverity.Error, m, id, cat);
}