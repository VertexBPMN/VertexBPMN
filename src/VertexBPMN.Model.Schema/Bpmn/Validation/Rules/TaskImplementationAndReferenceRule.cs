using System.Collections.Generic;
using System.Linq;
using VertexBPMN.Domain.Model.Bpmn;
using VertexBPMN.Domain.Model.Bpmn.Validation;

namespace VertexBPMN.Domain.Model.Bpmn.Validation.Rules;

/// <summary>
/// Validates task-specific required attributes and references (service/user/script/receive/callActivity).
/// </summary>
internal sealed class TaskImplementationAndReferenceRule : IBpmnSemanticRule
{
    public IEnumerable<ValidationDiagnostic> Evaluate(BpmnModel model, SemanticValidationContext ctx)
    {
        var diagnostics = new List<ValidationDiagnostic>();

        foreach (var task in model.Tasks)
        {
            if (task.Id is null) continue;
            switch (task)
            {
                case ServiceTask st when string.IsNullOrWhiteSpace(st.Implementation):
                    diagnostics.Add(Warning("BPMN140", $"ServiceTask '{st.Id}' missing implementation", st.Id, "Task"));
                    break;
                case ScriptTask sc when sc.Script == null || (sc.Script.Text == null || sc.Script.Text.All(string.IsNullOrWhiteSpace)):
                    diagnostics.Add(Warning("BPMN141", $"ScriptTask '{sc.Id}' empty script body", sc.Id, "Task"));
                    break;
                case ReceiveTask rt when rt.Instantiate && rt.Implementation is null:
                    diagnostics.Add(Warning("BPMN142", $"ReceiveTask '{rt.Id}' instantiate set but missing implementation/message correlation", rt.Id, "Task"));
                    break;
            }
        }

        // CallActivity instances may not be contained in model.Tasks list depending on parser classification.
        // Scan all activities collection if available.
        if (model.Activities != null)
        {
            foreach (var ca in model.Activities.OfType<CallActivity>())
            {
                if (ca.Id is null) continue;
                if (ca.CalledElement == null)
                    diagnostics.Add(Error("BPMN143", $"CallActivity '{ca.Id}' missing calledElementRef", ca.Id, "Task"));
            }
        }

        return diagnostics;
    }

    private static ValidationDiagnostic Error(string c, string m, string? id, string cat) => new(c, ValidationSeverity.Error, m, id, cat);
    private static ValidationDiagnostic Warning(string c, string m, string? id, string cat) => new(c, ValidationSeverity.Warning, m, id, cat);
}
