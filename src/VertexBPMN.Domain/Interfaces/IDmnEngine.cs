using VertexBPMN.Domain.Model.Dmn;

namespace VertexBPMN.Domain.Interfaces;

public interface IDmnEngine
{
    Task<Dictionary<string, object>> EvaluateDecisionAsync(DmnDecision decision, Dictionary<string, object> variables, CancellationToken cancellationToken = default);
}