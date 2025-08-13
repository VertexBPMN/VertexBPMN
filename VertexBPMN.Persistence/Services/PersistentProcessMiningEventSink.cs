using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using VertexBPMN.Core.Services;
using VertexBPMN.Persistence.Services;

namespace VertexBPMN.Persistence.Services
{
    /// <summary>
    /// Persistent process mining event sink (EF Core, PostgreSQL).
    /// </summary>
    public class PersistentProcessMiningEventSink : IProcessMiningEventSink
    {
    private readonly VertexBPMN.Persistence.Services.ProcessMiningEventDbContext _db;
    public PersistentProcessMiningEventSink(VertexBPMN.Persistence.Services.ProcessMiningEventDbContext db)
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
