using VertexBPMN.Core.Dmn;

namespace VertexBPMN.Core.Domain;

public interface IDmnEngine
{
    Task<Dictionary<string, object>> EvaluateDecisionAsync(DmnDecision decision, Dictionary<string, object> variables, CancellationToken cancellationToken = default);
}