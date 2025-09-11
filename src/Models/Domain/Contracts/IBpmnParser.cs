using System.Threading;
using System.Threading.Tasks;
using VertexBPMN.Domain.Modeling;

namespace VertexBPMN.Domain.Contracts
{
    public interface IBpmnParser
    {
        Task<BpmnModel> ParseAsync(string bpmnXml, CancellationToken cancellationToken = default);
        string Serialize(BpmnModel model);
    }
}
