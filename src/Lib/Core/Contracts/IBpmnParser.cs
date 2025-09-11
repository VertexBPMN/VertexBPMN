using VertexBPMN.Domain.Modeling;

namespace VertexBPMN.Core.Contracts
{
    public interface IBpmnParser
    {
        Task<BpmnModel> ParseAsync(string bpmnXml, CancellationToken cancellationToken = default);
        string Serialize(BpmnModel model);
    }
}
