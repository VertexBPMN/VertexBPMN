using VertexBPMN.Core.Modeling;

namespace VertexBPMN.Core.Contracts;

public interface IDmnParser
{
    Task<DmnDecision> ParseAsync(string dmnXml, CancellationToken cancellationToken = default);
}