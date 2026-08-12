using System.Collections.Concurrent;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.Application;

/// <summary>
/// In-memory process mining event sink for development/testing.
/// </summary>
public class ProcessMiningEventSink : IProcessMiningEventSink
{
    private readonly ConcurrentBag<ProcessMiningEvent> _events = new();

    public ValueTask<ProcessMiningEvent> EmitAsync(ProcessMiningEvent evt, CancellationToken cancellationToken = default)
    {
        _events.Add(evt);
        return new ValueTask<ProcessMiningEvent>(evt);
    }

    public IEnumerable<ProcessMiningEvent> GetAllEvents() => _events;

}