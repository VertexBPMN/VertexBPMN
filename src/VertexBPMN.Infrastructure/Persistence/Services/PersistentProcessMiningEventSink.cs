using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.Infrastructure.Persistence.Services
{
    /// <summary>
    /// Persistent process mining event sink (EF Core, PostgreSQL).
    /// </summary>
    public class PersistentProcessMiningEventSink : IProcessMiningEventSink
    {
    private readonly ProcessMiningEventDbContext _db;
    public PersistentProcessMiningEventSink(ProcessMiningEventDbContext db)
        {
            _db = db;
        }
        public async ValueTask EmitAsync(ProcessMiningEvent evt, CancellationToken cancellationToken = default)
        {
            await _db.Events.AddAsync(evt, cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);
        }
    }

    // ...existing code...
}
