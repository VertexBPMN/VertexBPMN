using VertexBPMN.Domain.Model.Bpmn;

namespace VertexBPMN.Domain.Interfaces
{
    public interface IBpmnParser
    {
        Task<BpmnModel> ParseAsync(string bpmnXml, CancellationToken cancellationToken = default);
        string Serialize(BpmnModel model);
    }
}
