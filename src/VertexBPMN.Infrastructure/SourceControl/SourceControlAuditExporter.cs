using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VertexBPMN.Application.SourceControl;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Infrastructure.Persistence;
using VertexBPMN.Infrastructure.Persistence.Services;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Infrastructure.SourceControl;

/// <summary>Commit-before-ack recovery: the central unique event ID prevents duplicate audit entries.</summary>
internal sealed class SourceControlAuditExporter(BpmnDbContext source, ProcessMiningEventDbContext destination,
	PersistentSourceControlStore store, ILogger<SourceControlAuditExporter>? logger = null)
{
	internal async Task<int> ExportAsync(CancellationToken cancellationToken)
	{
		var now = DateTimeOffset.UtcNow;
		var rows = await source.SourceControlOperations.AsNoTracking().Where(row => row.AuditPending
			&& (row.AuditRetryAfterUtcTicks == null || row.AuditRetryAfterUtcTicks <= now.UtcTicks))
			.OrderBy(row => row.UpdatedUtcTicks).Take(32).ToArrayAsync(cancellationToken);
		var exported = 0;
		foreach (var row in rows)
		{
			try
			{
				await PublishAsync(store.ReadCompletedAudit(row), cancellationToken);
				exported += await source.SourceControlOperations.Where(current => current.Id == row.Id && current.TenantId == row.TenantId
					&& current.AuditPending && current.State == row.State && current.ProtectedResult == row.ProtectedResult)
					.ExecuteUpdateAsync(update => update.SetProperty(current => current.AuditPending, false)
						.SetProperty(current => current.AuditExportedUtcTicks, DateTimeOffset.UtcNow.UtcTicks)
						.SetProperty(current => current.AuditRetryAfterUtcTicks, (long?)null), cancellationToken);
			}
			catch (OperationCanceledException) { throw; }
			catch (Exception)
			{
				logger?.LogWarning("Source-control audit {OperationId} remains pending; export will retry.", row.Id);
				await source.SourceControlOperations.Where(current => current.Id == row.Id && current.TenantId == row.TenantId && current.AuditPending)
					.ExecuteUpdateAsync(update => update.SetProperty(current => current.AuditRetryAfterUtcTicks, now.AddMinutes(1).UtcTicks), cancellationToken);
			}
		}
		return exported;
	}

	private async Task PublishAsync(AuditLog entry, CancellationToken cancellationToken)
	{
		var existing = await destination.AuditLogs.AsNoTracking().SingleOrDefaultAsync(row => row.SourceControlEventId == entry.SourceControlEventId, cancellationToken);
		if (existing is null)
		{
			destination.AuditLogs.Add(entry);
			try
			{ await destination.SaveChangesAsync(cancellationToken); return; }
			catch (DbUpdateException)
			{
				destination.Entry(entry).State = EntityState.Detached;
				existing = await destination.AuditLogs.AsNoTracking().SingleOrDefaultAsync(row => row.SourceControlEventId == entry.SourceControlEventId, cancellationToken);
				if (existing is null)
				{ throw; }
			}
		}
		if (existing.TenantId != entry.TenantId || existing.UserId != entry.UserId || existing.ResourceId != entry.ResourceId
			|| existing.Resource != entry.Resource || existing.CorrelationId != entry.CorrelationId || existing.StatusCode != entry.StatusCode
			|| existing.Action != entry.Action || existing.DetailsJson != entry.DetailsJson || existing.Timestamp != entry.Timestamp)
		{ throw new SourceControlSecurityException(SourceControlErrorCode.ContentUnsafe); }
	}
}
