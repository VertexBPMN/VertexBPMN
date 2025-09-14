using VertexBPMN.Domain.Entities.Modeling;

namespace VertexBPMN.Domain.Interfaces;

public interface IDmnParser
{
    Task<DmnDecision> ParseAsync(string dmnXml, CancellationToken cancellationToken = default);
}