using VertexBPMN.Domain.Entities.Modeling;

namespace VertexBPMN.Domain.Interfaces
{
    public interface IBpmnParser
    {
        Task<BpmnModel> ParseAsync(string bpmnXml, CancellationToken cancellationToken = default);
        string Serialize(BpmnModel model);
    }
}
