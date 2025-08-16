using System.Collections.Concurrent;

namespace VertexBPMN.Core.Services;

public sealed class ServiceTaskRegistry
{
    private readonly ConcurrentDictionary<string, IServiceTaskHandler> _handlers = new();

    public void Register(string implementation, IServiceTaskHandler handler)
    {
        if (string.IsNullOrWhiteSpace(implementation)) throw new ArgumentNullException(nameof(implementation));
        _handlers[implementation] = handler ?? throw new ArgumentNullException(nameof(handler));
    }

    public bool TryResolve(string implementation, out IServiceTaskHandler? handler)
    {
        return _handlers.TryGetValue(implementation ?? string.Empty, out handler);
    }
}