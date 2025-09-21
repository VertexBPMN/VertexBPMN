using VertexBPMN.Domain.Entities;

namespace VertexBPMN.Domain.Interfaces;

public interface IProcessMiningEventSink
{
    ValueTask<ProcessMiningEvent> EmitAsync(ProcessMiningEvent evt, CancellationToken cancellationToken = default);
}