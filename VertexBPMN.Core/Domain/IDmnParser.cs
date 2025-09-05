using VertexBPMN.Core.Dmn;

namespace VertexBPMN.Core.Domain;

public interface IDmnParser
{
    Task<DmnDecision> ParseAsync(string dmnXml, CancellationToken cancellationToken = default);
}