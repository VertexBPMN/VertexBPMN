using System.Collections.Concurrent;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.Infrastructure.Persistence.InMemory;

public sealed class InMemoryEventSink : IProcessMiningEventSink
{
    private readonly ConcurrentBag<ProcessMiningEvent> _events = new();
    public ValueTask EmitAsync(ProcessMiningEvent evt, CancellationToken cancellationToken = default)
    { _events.Add(evt); return ValueTask.CompletedTask; }
    public IEnumerable<ProcessMiningEvent> GetAll() => _events;
}