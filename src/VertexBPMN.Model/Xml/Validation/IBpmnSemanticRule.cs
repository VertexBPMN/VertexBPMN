using System.Collections.Generic;
using VertexBPMN.Domain.Model.Bpmn;
using VertexBPMN.Domain.Model.Bpmn.Infrastructure;
using VertexBPMN.Domain.Model.Validation;

namespace VertexBPMN.Domain.Model.Xml.Validation;

/// <summary>
/// Contract for a single BPMN semantic validation rule.
/// Each rule returns zero or more diagnostics (never null).
/// </summary>
public interface IBpmnSemanticRule
{
    IEnumerable<ValidationDiagnostic> Evaluate(BpmnModel model, SemanticValidationContext context);
}