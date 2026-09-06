using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Infrastructure.Persistence;
using VertexBPMN.Infrastructure.Persistence.Services;

namespace VertexBPMN.Infrastructure.Persistence.Services;

/// <summary>
/// Reads the most recent recorded task-IO snapshot output for a service-task
/// element, scoped to prior instances of the same process definition key
/// (tenant-isolated).
/// </summary>
public sealed class RecordedOutputQueryService(BpmnDbContext db) : IRecordedOutputQueryService
{
    public async Task<IReadOnlyDictionary<string, object>?> GetLastRecordedOutputAsync(
        string tenantId,
        string processDefinitionKey,
        string elementId,
        CancellationToken cancellationToken = default)
    {
        var definition = await db.ProcessDefinitions.AsNoTracking()
            .Where(p => p.Key == processDefinitionKey
                        && (p.TenantId == tenantId || p.TenantScope == "$global"))
            .OrderByDescending(p => p.Version)
            .FirstOrDefaultAsync(cancellationToken);
        if (definition is null)
            return null;

        var data = await db.HistoryEvents.AsNoTracking()
            .Where(e => e.EventType == TaskIoSnapshotRecorder.EventType
                        && e.ElementId == elementId
                        && e.TenantId == tenantId
                        && db.ProcessInstances.Any(pi =>
                            pi.Id == e.ProcessInstanceId
                            && pi.ProcessDefinitionId == definition.Id))
            .OrderByDescending(e => e.Timestamp)
            .Select(e => e.Data)
            .FirstOrDefaultAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(data))
            return null;

        return ParseOutput(data);
    }

    private static IReadOnlyDictionary<string, object>? ParseOutput(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("output", out var output)
                || output.ValueKind != JsonValueKind.Object)
                return null;

            var dict = new Dictionary<string, object>();
            foreach (var prop in output.EnumerateObject())
            {
                dict[prop.Name] = prop.Value.ValueKind switch
                {
                    JsonValueKind.String => prop.Value.GetString() ?? string.Empty,
                    JsonValueKind.Number => prop.Value.GetDecimal(),
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    _ => prop.Value.ValueKind == JsonValueKind.Null ? string.Empty : prop.Value.GetRawText()
                };
            }
            return dict;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
