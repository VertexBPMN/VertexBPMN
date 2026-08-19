using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Infrastructure.Persistence;

namespace VertexBPMN.Api.Controllers;

[ApiController]
[Route("api/case-definitions")]
[Authorize]
public sealed class CaseDefinitionController(BpmnDbContext db, ICmmnParser parser, IProcessEngine engine) : ControllerBase
{
    [HttpPost("deploy")]
    [Authorize(Policy = "ProcessManager")]
    public async Task<ActionResult<CaseDefinitionRecord>> Deploy([FromBody] DeployRequest request, CancellationToken cancellationToken)
    {
        var tenant = Tenant(request.TenantId);
        if (tenant is null) return Forbid();
        if (string.IsNullOrWhiteSpace(request.Key) || string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.CmmnXml)) return BadRequest();
        await parser.ParseAsync(request.CmmnXml, cancellationToken);
        if (await db.CaseDefinitions.AnyAsync(x => x.TenantId == tenant && x.Key == request.Key, cancellationToken)) return Conflict();
        var definition = new CaseDefinitionRecord { TenantId = tenant, Key = request.Key.Trim(), Name = request.Name.Trim(), CmmnXml = request.CmmnXml, CreatedAt = DateTime.UtcNow, LastModified = DateTime.UtcNow };
        db.CaseDefinitions.Add(definition); await db.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(Get), new { key = definition.Key, tenantId = tenant }, definition);
    }

    [HttpGet("{key}")]
    public async Task<ActionResult<CaseDefinitionRecord>> Get(string key, [FromQuery] string? tenantId, CancellationToken cancellationToken)
    {
        var tenant = Tenant(tenantId); if (tenant is null) return Forbid();
        var definition = await db.CaseDefinitions.AsNoTracking().SingleOrDefaultAsync(x => x.TenantId == tenant && x.Key == key, cancellationToken);
        return definition is null ? NotFound() : Ok(definition);
    }

    [HttpPost("{key}/start")]
    [Authorize(Policy = "ProcessManager")]
    public async Task<ActionResult<CaseRunResponse>> Start(string key, [FromBody] StartRequest request, CancellationToken cancellationToken)
    {
        var tenant = Tenant(request.TenantId); if (tenant is null) return Forbid();
        var definition = await db.CaseDefinitions.AsNoTracking().SingleOrDefaultAsync(x => x.TenantId == tenant && x.Key == key, cancellationToken);
        if (definition is null) return NotFound();
        var model = await parser.ParseAsync(definition.CmmnXml, cancellationToken);
        var trace = await engine.ExecuteCaseAsync(model, cancellationToken);
        return Ok(new CaseRunResponse(definition.Id, definition.Key, trace));
    }

    private string? Tenant(string? requested) => User.IsInRole("Admin") ? requested?.Trim() ?? "default" : User.FindFirstValue("tenant_id");
    public sealed record DeployRequest(string Key, string Name, string CmmnXml, string? TenantId);
    public sealed record StartRequest(string? TenantId);
    public sealed record CaseRunResponse(string CaseDefinitionId, string Key, IReadOnlyList<string> Trace);
}
