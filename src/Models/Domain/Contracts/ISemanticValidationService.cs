namespace VertexBPMN.Domain.Contracts
{
    public interface ISemanticValidationService
    {
        SemanticValidationResult ValidateBpmn(string bpmnXml);
        SemanticValidationResult ValidateDmn(string dmnXml);
    }
}
