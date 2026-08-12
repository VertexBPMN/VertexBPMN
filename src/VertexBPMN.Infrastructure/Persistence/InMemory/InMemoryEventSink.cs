using System.Collections.Concurrent;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.Infrastructure.Persistence.InMemory;

public sealed class InMemoryEventSink : IProcessMiningEventSink
{
    private readonly ConcurrentBag<ProcessMiningEvent> _events = new();
    public ValueTask<ProcessMiningEvent> EmitAsync(ProcessMiningEvent evt, CancellationToken cancellationToken = default)
    {
        _events.Add(evt);
        return new ValueTask<ProcessMiningEvent>(evt);
    }

    public IEnumerable<ProcessMiningEvent> GetAll() => _events;

    
}