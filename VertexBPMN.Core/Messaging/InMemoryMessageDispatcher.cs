using VertexBPMN.Core.Services;

namespace VertexBPMN.Core.Messaging;

/// <summary>
/// Einfacher Dispatcher, der bei vorhandener lokale Handler-Registry direkt aufruft.
/// Nützlich für Unit-Tests und single-node Deployments.
/// </summary>
public class InMemoryMessageDispatcher : IMessageDispatcher
{
    private readonly ServiceTaskRegistry _registry;

    public InMemoryMessageDispatcher(ServiceTaskRegistry registry)
    {
        _registry = registry;
    }

    public async Task DispatchServiceTaskAsync(string targetWorkerId, string implementation, IDictionary<string, string> attributes, IDictionary<string, object> variables, CancellationToken ct = default)
    {
        // Für Demo: Wenn ein lokaler Handler existiert, rufe ihn auf (synchron/async).
        if (_registry.TryResolve(implementation, out var handler))
        {
            await handler.ExecuteAsync(attributes, variables, ct).ConfigureAwait(false);
            return;
        }

        // Sonst: Simuliere Remote-Dispatch: hier einfach Log / No-op
        await Task.CompletedTask;
    }
}