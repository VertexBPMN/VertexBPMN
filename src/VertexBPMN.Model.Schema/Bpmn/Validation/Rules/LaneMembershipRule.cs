using System.Collections.Generic;
using System.Linq;
using VertexBPMN.Domain.Model.Bpmn;
using VertexBPMN.Domain.Model.Bpmn.Validation;

namespace VertexBPMN.Domain.Model.Bpmn.Validation.Rules;

/// <summary>
/// Validates lane FlowNode assignments: node appears in at most one lane; referenced node exists.
/// </summary>
internal sealed class LaneMembershipRule : IBpmnSemanticRule
{
    public IEnumerable<ValidationDiagnostic> Evaluate(BpmnModel model, SemanticValidationContext ctx)
    {
        var diagnostics = new List<ValidationDiagnostic>();
        if (model.Lanes == null) return diagnostics;

        var membership = new Dictionary<string, List<string>>();
        foreach (var lane in model.Lanes)
        {
            if (lane.Id is null) continue;
            foreach (var fn in lane.FlowNodeRef)
            {
                if (fn is null) continue;
                if (!membership.TryGetValue(fn, out var list))
                    membership[fn] = list = new List<string>();
                list.Add(lane.Id);
                if (!ctx.FlowNodesById.ContainsKey(fn))
                    diagnostics.Add(Error("BPMN160", $"Lane '{lane.Id}' references unknown FlowNode '{fn}'", lane.Id, "Lane"));
            }
        }

        foreach (var kv in membership.Where(kv => kv.Value.Count > 1))
        {
            diagnostics.Add(Warning("BPMN161", $"FlowNode '{kv.Key}' mapped to multiple lanes ({string.Join(", ", kv.Value)})", kv.Key, "Lane"));
        }

        return diagnostics;
    }

    private static ValidationDiagnostic Error(string c, string m, string? id, string cat) => new(c, ValidationSeverity.Error, m, id, cat);
    private static ValidationDiagnostic Warning(string c, string m, string? id, string cat) => new(c, ValidationSeverity.Warning, m, id, cat);
}
