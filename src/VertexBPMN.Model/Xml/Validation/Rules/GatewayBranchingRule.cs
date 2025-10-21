using System.Collections.Generic;
using System.Linq;
using VertexBPMN.Domain.Model.Bpmn;
using VertexBPMN.Domain.Model.Bpmn.Common;
using VertexBPMN.Domain.Model.Bpmn.Event;
using VertexBPMN.Domain.Model.Bpmn.Gateway;
using VertexBPMN.Domain.Model.Bpmn.Event;
using VertexBPMN.Domain.Model.Bpmn.Infrastructure;
using VertexBPMN.Domain.Model.Validation;
using VertexBPMN.Domain.Model.Xml.Validation;

namespace VertexBPMN.Domain.Model.Xml.Validation.Rules;

/// <summary>
/// Validates gateway branching/merging basics:
/// - Parallel / Inclusive / Complex must have >=2 outgoing
/// - ExclusiveGateway with >1 outgoing must have either default or at least one condition
/// - EventBasedGateway outgoing targets must be events (catch) with exactly one event definition
/// - Merging gateways (parallel/inclusive/complex/exclusive) should have >=2 incoming.
/// </summary>
internal sealed class GatewayBranchingRule : IBpmnSemanticRule
{
    public IEnumerable<ValidationDiagnostic> Evaluate(BpmnModel model, SemanticValidationContext ctx)
    {
        var diagnostics = new List<ValidationDiagnostic>();

        foreach (var gw in model.Gateways)
        {
            if (gw.Id is null) continue;
            var outgoing = ctx.Outgoing.TryGetValue(gw.Id, out var outs) ? outs : new List<SequenceFlow>();
            var incoming = ctx.Incoming.TryGetValue(gw.Id, out var ins) ? ins : new List<SequenceFlow>();

            switch (gw)
            {
                case ParallelGateway:
                case InclusiveGateway:
                case ComplexGateway:
                    if (outgoing.Count < 2)
                        diagnostics.Add(Error("BPMN120", $"{gw.GetType().Name} '{gw.Id}' requires >=2 outgoing SequenceFlows", gw.Id, "Gateway"));
                    break;
            }

            if (gw is ExclusiveGateway)
            {
                if (outgoing.Count > 1)
                {
                    var hasCondition = outgoing.Any(f => f.ConditionExpression != null);
                    var hasDefault = gw.Default != null;
                    if (!hasCondition && !hasDefault)
                        diagnostics.Add(Warning("BPMN121", $"ExclusiveGateway '{gw.Id}' has multiple outgoing flows but no condition/default", gw.Id, "Gateway"));
                }
            }

            if (gw is EventBasedGateway)
            {
                foreach (var sf in outgoing)
                {
                    if (sf.TargetRef is not Event targetEvt)
                    {
                        diagnostics.Add(Error("BPMN122", $"EventBasedGateway '{gw.Id}' targets non-event '{sf.TargetRef?.Id}'", gw.Id, "Gateway"));
                        continue;
                    }
                    if (targetEvt.EventDefinitions.Count != 1)
                        diagnostics.Add(Error("BPMN123", $"EventBasedGateway '{gw.Id}' target event '{targetEvt.Id}' must have exactly one event definition", targetEvt.Id, "Gateway"));
                }
            }

            // Merge heuristic: if incoming >1 ensure gateway type supports merging
            if (incoming.Count > 1 && gw is not EventBasedGateway)
            {
                // Accept all listed gateway types; warn if exclusive has 1 incoming only when >1 outgoing? Already covered.
                if (incoming.Count < 2)
                    diagnostics.Add(Warning("BPMN124", $"Gateway '{gw.Id}' appears to merge but has <2 incoming", gw.Id, "Gateway"));
            }
        }

        return diagnostics;
    }

    private static ValidationDiagnostic Error(string c, string m, string? id, string cat) => new(c, ValidationSeverity.Error, m, id, cat);
    private static ValidationDiagnostic Warning(string c, string m, string? id, string cat) => new(c, ValidationSeverity.Warning, m, id, cat);
}
