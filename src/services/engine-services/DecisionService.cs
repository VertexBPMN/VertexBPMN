using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using VertexBPMN.Core.Services.Dmn;

namespace VertexBPMN.Core.Services;

/// <summary>
/// In-memory DMN 1.4 DecisionService mit echtem Decision Table Parsing/Evaluation.
/// </summary>

public class DecisionService : IDecisionService
{
    public async IAsyncEnumerable<DecisionInstance> ListInstancesAsync(string? decisionKey = null, string? tenantId = null, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        yield break;
    }

    public async IAsyncEnumerable<DecisionDefinition> ListAsync(string? key = null, string? tenantId = null, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await System.Threading.Tasks.Task.CompletedTask;
        yield break;
    }


    private readonly ConcurrentDictionary<string, (string Name, string DmnXml, string? TenantId, DmnDecisionTable? Table)> _decisions = new();

    public ValueTask<DecisionResult> EvaluateDecisionByKeyAsync(string decisionKey, IDictionary<string, object> variables, string? tenantId = null, CancellationToken cancellationToken = default)
    {
        if (_decisions.TryGetValue(decisionKey, out var def) && def.Table != null)
        {
            var result = def.Table.Evaluate(variables.ToDictionary(kv => kv.Key, kv => kv.Value));
            return ValueTask.FromResult(new DecisionResult(result));
        }
        // Fallback: echo variables
        return ValueTask.FromResult(new DecisionResult(variables));
    }

    public ValueTask<DecisionDefinition?> GetDecisionByKeyAsync(string decisionKey, string? tenantId = null, CancellationToken cancellationToken = default)
    {
        if (_decisions.TryGetValue(decisionKey, out var def))
            return ValueTask.FromResult<DecisionDefinition?>(new DecisionDefinition(decisionKey, def.Name, def.DmnXml, def.TenantId));
        return ValueTask.FromResult<DecisionDefinition?>(null);
    }

    public ValueTask DeployAsync(string decisionKey, string name, string dmnXml, string? tenantId = null)
    {
        var table = DmnDecisionTable.Parse(dmnXml);
        _decisions[decisionKey] = (name, dmnXml, tenantId, table);
        return ValueTask.CompletedTask;
    }
}
