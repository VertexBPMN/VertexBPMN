using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VertexBPMN.Infrastructure.Persistence;
using VertexBPMN.Infrastructure.Persistence.Services;

namespace VertexBPMN.Api.Controllers;

/// <summary>
/// Reads persisted task-IO snapshots (<c>EventType = "TASK_IO_SNAPSHOT"</c>) for a
/// process instance + element. Data is already redacted at write time.
/// </summary>
[ApiController]
[Authorize]
public class TaskIoSnapshotController : ControllerBase
{
    private readonly BpmnDbContext _db;

    public TaskIoSnapshotController(BpmnDbContext db) => _db = db;

    [HttpGet]
    [Route("api/process-instances/{processInstanceId}/tasks/{elementId}/io-snapshots")]
    public async Task<ActionResult<IReadOnlyList<TaskIoSnapshotDto>>> List(
        Guid processInstanceId,
        string elementId,
        [FromQuery] string? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        var effectiveTenantId = ResolveTenantId(tenantId);
        if (effectiveTenantId is null)
            return Forbid("Cannot resolve tenant from token or query.");

        var rows = await _db.HistoryEvents.AsNoTracking()
            .Where(e => e.ProcessInstanceId == processInstanceId
                        && e.ElementId == elementId
                        && e.EventType == TaskIoSnapshotRecorder.EventType
                        && e.TenantId == effectiveTenantId)
            .OrderByDescending(e => e.Timestamp)
            .Select(e => new { e.Id, e.Timestamp, e.Data })
            .ToListAsync(cancellationToken);

        var result = rows.Select(r => new TaskIoSnapshotDto(
            r.Id,
            r.Timestamp,
            string.IsNullOrWhiteSpace(r.Data)
                ? JsonSerializer.Deserialize<JsonElement>("{}")
                : JsonSerializer.Deserialize<JsonElement>(r.Data))).ToList();

        return result;
    }

    private string? ResolveTenantId(string? explicitTenantId)
    {
        if (!string.IsNullOrWhiteSpace(explicitTenantId))
            return explicitTenantId;

        var claim = User.FindFirst("tenant_id") ??
                    User.FindFirst("tenantid") ??
                    User.FindFirst(ClaimTypes.NameIdentifier);
        return claim?.Value;
    }

    public sealed record TaskIoSnapshotDto(Guid Id, DateTime Timestamp, JsonElement Data);
}
