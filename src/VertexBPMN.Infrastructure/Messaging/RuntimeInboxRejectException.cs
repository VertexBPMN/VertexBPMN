namespace VertexBPMN.Infrastructure.Messaging;

/// <summary>
/// Thrown by a business handler to permanently reject a message (-> DLQ). Any other exception is treated
/// as a transient, retryable failure.
/// </summary>
public sealed class RuntimeInboxRejectException : Exception
{
    public RuntimeInboxRejectException(string reason, Exception? inner) : base(reason, inner) { }

    public RuntimeInboxRejectException()
    {
    }

    public RuntimeInboxRejectException(string? message) : base(message)
    {
    }
}
