using System.Text.Json;

namespace VertexBPMN.AgentWorker;

internal sealed class AgentRuntimeException(string code, string message, Exception? inner = null)
    : InvalidOperationException(message, inner)
{
    public string Code { get; } = code;
}
