using VertexBPMN.Core.Cmmn;

namespace VertexBPMN.Core.Domain;

public interface ICmmnParser
{
    Task<CaseModel> ParseAsync(string cmmnXml, CancellationToken cancellationToken = default);
}