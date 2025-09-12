using System.Threading;
using System.Threading.Tasks;
using VertexBPMN.Domain.Modeling;

namespace VertexBPMN.Domain.Contracts;

public interface ICmmnParser
{
    Task<CaseModel> ParseAsync(string cmmnXml, CancellationToken cancellationToken = default);
}