using VertexBPMN.Domain.Entities.Modeling;

namespace VertexBPMN.Domain.Interfaces;

public interface ICmmnParser
{
    Task<CaseModel> ParseAsync(string cmmnXml, CancellationToken cancellationToken = default);
}