using VertexBPMN.Domain.Entities.Modeling;

namespace VertexBPMN.Domain.Interfaces;

public interface IDmnEngine
{
    Task<Dictionary<string, object>> EvaluateDecisionAsync(DmnDecision decision, Dictionary<string, object> variables, CancellationToken cancellationToken = default);
}