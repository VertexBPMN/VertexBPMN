using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using VertexBPMN.Infrastructure.Persistence.Services;

namespace VertexBPMN.Api.Controllers;

[ApiController]
[Route("api/audit")]
[Authorize(Policy = "ReadOnly")]
public sealed class AuditController(ProcessMiningEventDbContext db) : ControllerBase
{
    [HttpGet("logs")]
    public async Task<ActionResult> GetLogs(
        [FromQuery] string? action = null,
        [FromQuery] DateTimeOffset? from = null,
        [FromQuery] DateTimeOffset? to = null,
        [FromQuery] int limit = 100,
        CancellationToken cancellationToken = default)
    {
        var tenantId = User.FindFirstValue("tenant_id");
        limit = Math.Clamp(limit, 1, 500);
        var query = db.AuditLogs.AsNoTracking().Where(log => log.TenantId == tenantId);
        if (!string.IsNullOrWhiteSpace(action))
            query = query.Where(log => log.Action == action);
        if (from.HasValue)
            query = query.Where(log => log.Timestamp >= from.Value);
        if (to.HasValue)
            query = query.Where(log => log.Timestamp <= to.Value);

        return Ok(await query.OrderByDescending(log => log.Timestamp).Take(limit).ToListAsync(cancellationToken));
    }
}