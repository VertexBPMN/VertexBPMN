using VertexBPMN.Core.Modeling;

namespace VertexBPMN.Core.Contracts;

public interface ICmmnParser
{
    Task<CaseModel> ParseAsync(string cmmnXml, CancellationToken cancellationToken = default);
}