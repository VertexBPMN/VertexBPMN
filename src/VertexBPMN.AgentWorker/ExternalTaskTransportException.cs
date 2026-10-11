using System.Text.Json;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.AgentWorker;

internal sealed class ExternalTaskTransportException(string message, TimeSpan? retryAfter = null, Exception? inner = null)
    : HttpRequestException(message, inner)
{
    public TimeSpan? RetryAfter { get; } = retryAfter;
}
