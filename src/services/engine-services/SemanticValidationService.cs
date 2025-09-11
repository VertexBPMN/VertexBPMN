using VertexBPMN.Core.Domain;
using System.Collections.Generic;
namespace VertexBPMN.Core.Services
{
    public class SemanticValidationService : ISemanticValidationService
    {
        public SemanticValidationResult ValidateBpmn(string bpmnXml)
        {
            var errors = new List<string>();
            var warnings = new List<string>();
            var suggestions = new List<string>();
            bool hasStartEvent = bpmnXml.Contains("<startEvent");
            bool hasEndEvent = bpmnXml.Contains("<endEvent");
            if (!hasStartEvent) errors.Add("No start event found.");
            if (!hasEndEvent) errors.Add("No end event found.");
            if (bpmnXml.Contains("<exclusiveGateway") && !bpmnXml.Contains("<sequenceFlow"))
                errors.Add("Exclusive gateway without sequence flows.");
            if (bpmnXml.Contains("<userTask") && !bpmnXml.Contains("<formKey"))
                warnings.Add("User task without formKey.");
            if (bpmnXml.Contains("<serviceTask") && !bpmnXml.Contains("<implementation"))
                suggestions.Add("Service task should specify implementation.");
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
