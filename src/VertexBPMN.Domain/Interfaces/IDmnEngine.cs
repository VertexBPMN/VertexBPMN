using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using VertexBPMN.Domain.Modeling;

namespace VertexBPMN.Domain.Contracts;

public interface IDmnEngine
{
    Task<Dictionary<string, object>> EvaluateDecisionAsync(DmnDecision decision, Dictionary<string, object> variables, CancellationToken cancellationToken = default);
}