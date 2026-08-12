using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.Infrastructure.Persistence.Services
{
    /// <summary>
    /// Null implementation of IProcessMiningEventSink for production/persistence layer (no-op).
    /// </summary>
    public class NullProcessMiningEventSink : IProcessMiningEventSink
    {
        public ValueTask<ProcessMiningEvent> EmitAsync(ProcessMiningEvent evt, CancellationToken cancellationToken = default)
            => new ValueTask<ProcessMiningEvent>(evt);
    }
}
