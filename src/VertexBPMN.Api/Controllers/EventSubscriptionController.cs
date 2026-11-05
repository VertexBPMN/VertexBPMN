using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using VertexBPMN.Infrastructure.Operational;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Infrastructure.Persistence;

namespace VertexBPMN.Api.Controllers;

#pragma warning disable CA1515 // Public MVC controller and HTTP response contract.

[ApiController]
[Route("api/event-subscriptions")]
[Authorize(Policy = "TenantReadOnly")]
public sealed class EventSubscriptionController(BpmnDbContext db, IConfiguration configuration) : ControllerBase
{
    [HttpGet("{id:guid}/events")]
    public async Task<ActionResult<EventStreamPage>> Read(Guid id, [FromQuery] string? after = null,
        [FromQuery] Guid? definitionId = null, CancellationToken cancellationToken = default)
    {
        if (!configuration.GetValue("Runtime:EventSubscriptions:Enabled", false))
        {
            return NotFound();
        }
        var tenant = User.FindFirst("tenant_id")?.Value;
        if (string.IsNullOrWhiteSpace(tenant))
        {
            return Forbid();
        }
        if (!await db.EventStreamSubscriptions.AnyAsync(x => x.Id == id && x.TenantId == tenant, cancellationToken))
        {
            return NotFound();
        }
        long position = 0;
        if (after is not null && !EventStreamCursor.TryDecode(after, id, definitionId, out position))
        {
            return BadRequest();
        }
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        if (db.Database.IsNpgsql())
        {
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT 1 FROM \"EventStreamSubscriptions\" WHERE \"Id\" = {id} AND \"TenantId\" = {tenant} FOR SHARE", cancellationToken);
        }
        var expired = await db.EventStreamSubscriptions.Where(x => x.Id == id)
            .Select(x => x.ExpiredThrough).SingleAsync(cancellationToken);
        if (position < expired)
        {
            return StatusCode(410, new { Code = "event_cursor_expired" });
        }
        var events = await db.EventStreamDeliveries.AsNoTracking()
            .Where(x => x.SubscriptionId == id && x.TenantId == tenant && x.Id > position)
            .Where(x => definitionId == null || x.DefinitionId == definitionId)
            .OrderBy(x => x.Id).Take(100).ToListAsync(cancellationToken);
        return new EventStreamPage(events, events.Count == 0 ? after : EventStreamCursor.Encode(id, events[^1].Id, definitionId));
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<EventStreamSubscription>>> List(CancellationToken cancellationToken)
    {
        if (!FeaturesEnabled())
        {
            return NotFound();
        }

        var tenant = Tenant(HttpContext);
        if (tenant is null)
        {
            return Forbid();
        }

        var subs = await db.EventStreamSubscriptions
            .Where(x => x.TenantId == tenant).OrderBy(x => x.Name).ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);
        return Ok(subs);
    }

    [HttpPost]
    [Authorize(Policy = "TenantSubscriptionManager")]
    public async Task<ActionResult<EventStreamSubscription>> Create(SubscriptionCreateRequest body, CancellationToken cancellationToken)
    {
        if (!FeaturesEnabled())
        {
            return NotFound();
        }

        var tenant = Tenant(HttpContext);
        if (tenant is null)
        {
            return Forbid();
        }

        var sub = new EventStreamSubscription { Id = Guid.NewGuid(), TenantId = tenant, Name = body.Name ?? string.Empty };
        db.EventStreamSubscriptions.Add(sub);
        await db.SaveChangesAsync(cancellationToken);
        return Ok(sub);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "TenantSubscriptionManager")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        if (!FeaturesEnabled())
        {
            return NotFound();
        }

        var tenant = Tenant(HttpContext);
        if (tenant is null)
        {
            return Forbid();
        }

        var sub = await db.EventStreamSubscriptions.FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenant, cancellationToken);
        if (sub is null)
        {
            return NotFound();
        }

        db.EventStreamSubscriptions.Remove(sub);
        await db.SaveChangesAsync(cancellationToken);
        return Ok(sub);
    }

    private static string? Tenant(HttpContext http)
    {
        var tenant = http.User.FindFirst("tenant_id")?.Value;
        return string.IsNullOrWhiteSpace(tenant) ? null : tenant;
    }

    private bool FeaturesEnabled() =>
        configuration.GetValue("Runtime:EventSubscriptions:Enabled", false);

    public sealed record EventStreamPage(IReadOnlyList<EventStreamDelivery> Events, string? NextCursor);

    public sealed record SubscriptionCreateRequest(string? Name);
}
