using System.Threading;
using System.Threading.Tasks;
using VertexBPMN.Domain.Modeling;

namespace VertexBPMN.Domain.Contracts;

public interface IDmnParser
{
    Task<DmnDecision> ParseAsync(string dmnXml, CancellationToken cancellationToken = default);
}