using System.Collections.Generic;
using System.Linq;
using VertexBPMN.Domain.Model.Bpmn;
using VertexBPMN.Domain.Model.Bpmn.Common;
using VertexBPMN.Domain.Model.Bpmn.Gateway;
using VertexBPMN.Domain.Model.Bpmn.Infrastructure;
using VertexBPMN.Domain.Model.Bpmn.Process;
using VertexBPMN.Domain.Model.Validation;
using VertexBPMN.Domain.Model.Xml.Validation;

namespace VertexBPMN.Domain.Model.Xml.Validation.Rules;

/// <summary>
/// Ensures default sequence flow is unique per source and conditional flows are not mixed incorrectly.
/// </summary>
internal sealed class ConditionalAndDefaultFlowRule : IBpmnSemanticRule
{
    public IEnumerable<ValidationDiagnostic> Evaluate(BpmnModel model, SemanticValidationContext ctx)
    {
        var diagnostics = new List<ValidationDiagnostic>();

        // For each source node: only one default; conditions only allowed on allowed gateways/activities (SequenceFlowConditionsRule covers) but ensure at least one condition when multiple conditional flows present
        foreach (var node in ctx.FlowNodes)
        {
            if (node.Id is null) continue;
            var outgoing = ctx.Outgoing.TryGetValue(node.Id, out var outs) ? outs : new List<SequenceFlow>();
            if (outgoing.Count <= 1) continue;
            var conditional = outgoing.Where(sf => sf.ConditionExpression != null).ToList();
            if (node is Activity && conditional.Count > 0 && conditional.Count == outgoing.Count)
                diagnostics.Add(Warning("BPMN190", $"Activity '{node.Id}' has all outgoing flows conditional; consider default for clarity", node.Id, "SequenceFlow"));
        }

        // Gateway-level uniqueness of default
        foreach (var gw in model.Gateways.OfType<ExclusiveGateway>())
        {
            if (gw.Id is null) continue;
            var outgoing = ctx.Outgoing.TryGetValue(gw.Id, out var outs) ? outs : new List<SequenceFlow>();
            var defaultId = gw.Default?.Id;
            if (defaultId != null && outgoing.Count(sf => sf.Id == defaultId) != 1)
                diagnostics.Add(Error("BPMN191", $"ExclusiveGateway '{gw.Id}' default flow '{defaultId}' not found among outgoing", gw.Id, "Gateway"));
        }

        return diagnostics;
    }

    private static ValidationDiagnostic Error(string c, string m, string? id, string cat) => new(c, ValidationSeverity.Error, m, id, cat);
    private static ValidationDiagnostic Warning(string c, string m, string? id, string cat) => new(c, ValidationSeverity.Warning, m, id, cat);
}
