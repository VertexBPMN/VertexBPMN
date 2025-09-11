using VertexBPMN.Domain;

namespace VertexBPMN.Core.Contracts
{
    public interface ISemanticValidationService
    {
        SemanticValidationResult ValidateBpmn(string bpmnXml);
        SemanticValidationResult ValidateDmn(string dmnXml);
    }
}
