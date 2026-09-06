using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using VertexBPMN.Application;
using VertexBPMN.Application.Connectors;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Infrastructure.Persistence;

namespace VertexBPMN.Infrastructure.Persistence.Services;

/// <summary>
/// Persists redacted task-IO snapshots as <c>HistoryEvent</c> rows with
/// <c>EventType = "TASK_IO_SNAPSHOT"</c>, gated behind the global feature flag
/// <c>task-io-snapshots</c> (opt-in; performance-neutral when disabled).
/// </summary>
public sealed class TaskIoSnapshotRecorder(
    BpmnDbContext db,
    ConnectorRedactionPolicy redaction) : ITaskIoSnapshotRecorder
{
    public const string FeatureFlagName = "task-io-snapshots";
    public const string EventType = "TASK_IO_SNAPSHOT";

    public async Task RecordAsync(
        Guid processInstanceId,
        string elementId,
        string tenantId,
        IReadOnlyDictionary<string, object> input,
        IReadOnlyDictionary<string, object>? output,
        bool success,
        string? errorMessage,
        CancellationToken cancellationToken = default)
    {
        if (!await db.FeatureFlags.AsNoTracking()
                .AnyAsync(f => f.Name == FeatureFlagName && f.Enabled, cancellationToken))
            return;

        var redactedInput = redaction.Redact(input);
        var redactedOutput = output is null ? null : redaction.Redact(output);

        db.HistoryEvents.Add(new HistoryEvent
        {
            Id = Guid.NewGuid(),
            ProcessInstanceId = processInstanceId,
            EventType = EventType,
            Timestamp = DateTime.UtcNow,
            ElementId = elementId,
            TenantId = tenantId,
            Data = JsonSerializer.Serialize(new
            {
                input = redactedInput,
                output = redactedOutput,
                success,
                errorMessage
            })
        });

        await db.SaveChangesAsync(cancellationToken);
    }
}
