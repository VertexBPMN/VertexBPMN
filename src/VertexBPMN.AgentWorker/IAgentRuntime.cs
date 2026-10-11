using System.Text.Json;

namespace VertexBPMN.AgentWorker;

/// <summary>
/// Provider-neutral, bounded model runtime. It has no file, shell, network-tool or process API.
/// </summary>
internal interface IAgentRuntime
{
    ValueTask<AgentRuntimeResponse> CompleteAsync(
        AgentRuntimeRequest request,
        CancellationToken cancellationToken);
}
