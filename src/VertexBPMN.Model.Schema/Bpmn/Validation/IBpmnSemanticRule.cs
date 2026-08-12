using System.Collections.Generic;
using VertexBPMN.Domain.Model.Bpmn;

namespace VertexBPMN.Domain.Model.Bpmn.Validation;

/// <summary>
/// <summary>
/// Contract for a single BPMN semantic validation rule.
/// Each rule returns zero or more diagnostics (never null).
/// </summary>
public interface IBpmnSemanticRule
{
    IEnumerable<ValidationDiagnostic> Evaluate(BpmnModel model, SemanticValidationContext context);
}