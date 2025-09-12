using System;
using System.Collections.Concurrent;
using VertexBPMN.Domain.Contracts;
using VertexBPMN.Domain.Exceptions;

namespace VertexBPMN.EngineServices;

public sealed class ServiceTaskRegistry : IServiceTaskRegistry
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
    public IServiceTaskHandler GetHandler(string type)
    {
        if (TryResolve(type, out var handler))
            return handler;
        throw new DistributedTokenException($"No handler registered for service task type: {type}");
    }
}