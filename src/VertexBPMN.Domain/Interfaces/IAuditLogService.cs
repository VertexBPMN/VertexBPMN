using VertexBPMN.Domain.Entities;

namespace VertexBPMN.Domain.Interfaces;

public interface IAuditLogService
{
    Task<AuditLog> RecordAsync(AuditLog entry, CancellationToken cancellationToken = default);
}