using VertexBPMN.Domain.Model.Dmn;

namespace VertexBPMN.Domain.Interfaces;

public interface IDmnParser
{
    Task<DmnDecision> ParseAsync(string dmnXml, CancellationToken cancellationToken = default);
}