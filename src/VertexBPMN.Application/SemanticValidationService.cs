using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Domain.Model.Bpmn;
using VertexBPMN.Domain.Exceptions;
using System.Xml;

namespace VertexBPMN.Application
{
    public class SemanticValidationService(IBpmnParser parser) : ISemanticValidationService
    {
        public async Task<SemanticValidationResult> ValidateBpmnAsync(string bpmnXml, CancellationToken cancellationToken = default)
        {
            var errors = new List<string>();
            var warnings = new List<string>();
            var suggestions = new List<string>();
            try
            {
                var model = await parser.ParseAsync(bpmnXml, cancellationToken).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(model.ProcessId)) errors.Add("The BPMN model does not contain a process id.");
                foreach (var diagnostic in (model.ValidationDiagnostics ?? []).Concat(BpmnDeploymentValidator.Validate(model)))
                {
                    var message = $"{diagnostic.Code}: {diagnostic.Message}";
                    if (diagnostic.Severity >= ValidationSeverity.Error) errors.Add(message);
                    else if (diagnostic.Severity == ValidationSeverity.Warning) warnings.Add(message);
                    else suggestions.Add(message);
                }
            }
            catch (Exception exception) when (exception is XmlException or BpmnParseException or BpmnValidationException or SecurityException or ArgumentException)
            {
                errors.Add(exception.Message);
            }
            bool isValid = errors.Count == 0;
            return new SemanticValidationResult
            {
                IsValid = isValid,
                Errors = errors,
                Warnings = warnings,
                Suggestions = suggestions
            };
        }

        public SemanticValidationResult ValidateDmn(string dmnXml)
        {
            var errors = new List<string>();
            var warnings = new List<string>();
            var suggestions = new List<string>();
            bool hasDecisionTable = dmnXml.Contains("<decisionTable");
            if (!hasDecisionTable) errors.Add("No decision table found.");
            if (!dmnXml.Contains("<input")) warnings.Add("No input variables defined.");
            if (!dmnXml.Contains("<output")) warnings.Add("No output variables defined.");
            bool isValid = errors.Count == 0;
            return new SemanticValidationResult
            {
                IsValid = isValid,
                Errors = errors,
                Warnings = warnings,
                Suggestions = suggestions
            };
        }
    }
}
