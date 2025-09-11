using VertexBPMN.Core.Modeling;

namespace VertexBPMN.Core.Contracts;

public interface IDmnEngine
{
    Task<Dictionary<string, object>> EvaluateDecisionAsync(DmnDecision decision, Dictionary<string, object> variables, CancellationToken cancellationToken = default);
}