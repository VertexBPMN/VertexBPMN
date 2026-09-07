using VertexBPMN.Domain.Entities;

namespace VertexBPMN.Domain.Interfaces
{
    public interface ISemanticValidationService
    {
        Task<SemanticValidationResult> ValidateBpmnAsync(string bpmnXml, CancellationToken cancellationToken = default);
        SemanticValidationResult ValidateDmn(string dmnXml);
    }
}
