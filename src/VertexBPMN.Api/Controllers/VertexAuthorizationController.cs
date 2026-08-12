using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Infrastructure.Persistence;

namespace VertexBPMN.Api.Controllers;

using Microsoft.AspNetCore.Authorization;
[ApiController]
[Route("api/vertex/authorization")]
[Authorize(Policy = "ReadOnly")]
public class VertexAuthorizationController : ControllerBase
{
    private readonly IIdentityService _identityService;
    private readonly BpmnDbContext _db;

    public VertexAuthorizationController(IIdentityService identityService, BpmnDbContext db)
    {
        _identityService = identityService;
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AuthorizationDto>>> GetAll(CancellationToken cancellationToken)
    {
        var authorizations = new List<AuthorizationDto>();
        await foreach (var authorization in _identityService.ListAuthorizationsAsync(cancellationToken, CurrentTenantId()))
        {
            authorizations.Add(new AuthorizationDto
            {
                Id = authorization.Id,
                UserId = authorization.UserId,
                GroupId = authorization.GroupId,
                Resource = authorization.Resource,
                Permissions = authorization.Permissions
            });
        }

        return Ok(authorizations);
    }

    [HttpPost]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<AuthorizationDto>> Create(
        [FromBody] AuthorizationDto request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.UserId) || string.IsNullOrWhiteSpace(request.GroupId) ||
            string.IsNullOrWhiteSpace(request.Resource) || string.IsNullOrWhiteSpace(request.Permissions))
            return ValidationProblem("UserId, GroupId, Resource and Permissions are required.");

        var group = await _db.IdentityGroups.SingleOrDefaultAsync(group => group.Id == request.GroupId, cancellationToken);
        var user = await _db.Users.SingleOrDefaultAsync(user => user.Id == request.UserId, cancellationToken);
        if (group is null || user is null || (group.TenantId is not null && user.TenantId != group.TenantId))
            return NotFound();

        var authorization = new IdentityAuthorizationRecord
        {
            UserId = request.UserId,
            GroupId = request.GroupId,
            Resource = request.Resource.Trim(),
            Permissions = request.Permissions.Trim(),
            TenantId = group.TenantId
        };
        _db.IdentityAuthorizations.Add(authorization);
        await _db.SaveChangesAsync(cancellationToken);
        request.Id = authorization.Id;
        return CreatedAtAction(nameof(GetAll), request);
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Delete(string id, CancellationToken cancellationToken)
    {
        var authorization = await _db.IdentityAuthorizations.FindAsync([id], cancellationToken);
        if (authorization is null) return NotFound();
        _db.IdentityAuthorizations.Remove(authorization);
        await _db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private string? CurrentTenantId() => User.IsInRole("Admin")
        ? null
        : User.FindFirstValue("tenant_id");

    public class AuthorizationDto
    {
        public string Id { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string GroupId { get; set; } = string.Empty;
        public string Resource { get; set; } = string.Empty;
        public string Permissions { get; set; } = string.Empty;
    }
}
