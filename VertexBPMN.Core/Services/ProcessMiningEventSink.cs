using System.Collections.Concurrent;

namespace VertexBPMN.Core.Services;

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
