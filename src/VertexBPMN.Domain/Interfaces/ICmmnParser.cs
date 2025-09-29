
using VertexBPMN.Domain.Model.Cmn;

namespace VertexBPMN.Domain.Interfaces;

public interface ICmmnParser
{
    Task<CaseModel> ParseAsync(string cmmnXml, CancellationToken cancellationToken = default);
}