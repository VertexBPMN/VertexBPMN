using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using VertexBPMN.Domain.Contracts;

namespace VertexBPMN.EngineServices;

/// <summary>
/// In-memory process mining event sink for development/testing.
/// </summary>
public class ProcessMiningEventSink : IProcessMiningEventSink
{
    private readonly ConcurrentBag<ProcessMiningEvent> _events = new();

    public ValueTask EmitAsync(ProcessMiningEvent evt, CancellationToken cancellationToken = default)
    {
        _events.Add(evt);
        return ValueTask.CompletedTask;
    }

    public IEnumerable<ProcessMiningEvent> GetAllEvents() => _events;
}