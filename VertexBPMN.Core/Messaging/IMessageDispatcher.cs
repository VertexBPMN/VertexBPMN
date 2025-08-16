namespace VertexBPMN.Core.Messaging;

/// <summary>
/// Abstraktion, um einen ServiceTask remote auszuführen (z. B. via RabbitMQ, Redis, HTTP, etc.).
/// Die Methode gibt optional ein Ergebnis-Map zurück (hier: void, weil ServiceTask direkt Variablen ändern kann).
/// </summary>
public interface IMessageDispatcher
{
    /// <summary>
    /// Dispatch a remote service task execution request to a worker node.
    /// Returns when dispatch is accepted (not necessarily when remote finished).
    /// For testing you can implement sync behavior that actually executes the handler.
    /// </summary>
    Task DispatchServiceTaskAsync(
        string targetWorkerId,
        string implementation,
        IDictionary<string, string> attributes,
        IDictionary<string, object> variables,
        CancellationToken ct = default);
}