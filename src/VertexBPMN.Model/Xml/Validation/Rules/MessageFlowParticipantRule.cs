using System.Collections.Generic;
using System.Linq;
using VertexBPMN.Domain.Model.Bpmn;
using VertexBPMN.Domain.Model.Bpmn.Collaboration;
using VertexBPMN.Domain.Model.Bpmn.Infrastructure;
using VertexBPMN.Domain.Model.Bpmn.Process;
using VertexBPMN.Domain.Model.Bpmn.Process;
using VertexBPMN.Domain.Model.Validation;
using VertexBPMN.Domain.Model.Xml.Validation;

namespace VertexBPMN.Domain.Model.Xml.Validation.Rules;

/// <summary>
/// Validates message flow participant and interaction node references.
/// </summary>
internal sealed class MessageFlowParticipantRule : IBpmnSemanticRule
{
    public IEnumerable<ValidationDiagnostic> Evaluate(BpmnModel model, SemanticValidationContext ctx)
    {
        var diagnostics = new List<ValidationDiagnostic>();

        if (model.MessageFlows != null)
        {
            foreach (var mf in model.MessageFlows)
            {
                if (mf.Id is null) continue;
                if (mf.SourceRef == null)
                    diagnostics.Add(Error("BPMN150", $"MessageFlow '{mf.Id}' missing sourceRef", mf.Id, "MessageFlow"));
                if (mf.TargetRef == null)
                    diagnostics.Add(Error("BPMN151", $"MessageFlow '{mf.Id}' missing targetRef", mf.Id, "MessageFlow"));
                if (mf.MessageRef == null)
                    diagnostics.Add(Warning("BPMN152", $"MessageFlow '{mf.Id}' missing messageRef", mf.Id, "MessageFlow"));
            }
        }

        if (model.Participants != null && model.ProcessDefinitions?.RootElements != null)
        {
            var processIds = model.ProcessDefinitions.RootElements.OfType<Process>().Select(p => p.Id).Where(id => id != null).ToHashSet()!;
            foreach (var part in model.Participants)
            {
                if (part.ProcessRef?.Id is string pid && !processIds.Contains(pid))
                    diagnostics.Add(Error("BPMN153", $"Participant '{part.Id}' references unknown process '{pid}'", part.Id, "Collaboration"));
            }
        }

        return diagnostics;
    }

    private static ValidationDiagnostic Error(string c, string m, string? id, string cat) => new(c, ValidationSeverity.Error, m, id, cat);
    private static ValidationDiagnostic Warning(string c, string m, string? id, string cat) => new(c, ValidationSeverity.Warning, m, id, cat);
}
