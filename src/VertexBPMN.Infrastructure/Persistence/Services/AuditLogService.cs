using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.Infrastructure.Persistence.Services;

public sealed class AuditLogService(ProcessMiningEventDbContext db) : IAuditLogService
{
    public async Task<AuditLog> RecordAsync(AuditLog entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (string.IsNullOrWhiteSpace(entry.Action))
            throw new ArgumentException("Audit action is required", nameof(entry));

        entry.Timestamp = entry.Timestamp == default ? DateTimeOffset.UtcNow : entry.Timestamp.ToUniversalTime();
        await db.AuditLogs.AddAsync(entry, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return entry;
    }
}