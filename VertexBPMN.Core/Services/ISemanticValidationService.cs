using VertexBPMN.Core.Domain;
namespace VertexBPMN.Core.Services
{
    public interface ISemanticValidationService
    {
        SemanticValidationResult ValidateBpmn(string bpmnXml);
        SemanticValidationResult ValidateDmn(string dmnXml);
    }
}
