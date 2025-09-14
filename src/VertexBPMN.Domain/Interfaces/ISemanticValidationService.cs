using VertexBPMN.Domain.Entities;

namespace VertexBPMN.Domain.Interfaces
{
    public interface ISemanticValidationService
    {
        SemanticValidationResult ValidateBpmn(string bpmnXml);
        SemanticValidationResult ValidateDmn(string dmnXml);
    }
}
