using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using VertexBPMN.Domain.Contracts;

namespace VertexBPMN.EngineServices;

public sealed class InMemoryEventSink : IProcessMiningEventSink
{
    private readonly ConcurrentBag<ProcessMiningEvent> _events = new();
    public ValueTask EmitAsync(ProcessMiningEvent evt, CancellationToken cancellationToken = default)
    { _events.Add(evt); return ValueTask.CompletedTask; }
    public IEnumerable<ProcessMiningEvent> GetAll() => _events;
}