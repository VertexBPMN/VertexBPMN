using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using VertexBPMN.Infrastructure.Persistence;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.Api.Controllers;

using Microsoft.AspNetCore.Authorization;
[ApiController]
[Route("api/vertex/group")]
[Authorize(Policy = "ReadOnly")]
public class VertexGroupController : ControllerBase
{
    private readonly IIdentityService _identityService;
    private readonly BpmnDbContext _db;

    public VertexGroupController(IIdentityService identityService, BpmnDbContext db)
    {
        _identityService = identityService;
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<GroupDto>>> GetAll(CancellationToken cancellationToken)
    {
        var groups = new List<GroupDto>();
        await foreach (var group in _identityService.ListGroupsAsync(cancellationToken, CurrentTenantId()))
            groups.Add(new GroupDto { Id = group.Id, Name = group.Name, Type = group.Type });
        return Ok(groups);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<GroupDto>> GetById(string id, CancellationToken cancellationToken)
    {
        var group = await _identityService.GetGroupByIdAsync(id, cancellationToken, CurrentTenantId());
        return group is null
            ? NotFound()
            : Ok(new GroupDto { Id = group.Id, Name = group.Name, Type = group.Type });
    }

    private string? CurrentTenantId() => User.IsInRole("Admin")
        ? null
        : User.FindFirstValue("tenant_id");

    [HttpPost]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<GroupDto>> Create([FromBody] GroupWriteDto request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return ValidationProblem("Group name is required.");

        var group = new IdentityGroupRecord
        {
            Name = request.Name.Trim(),
            Type = string.IsNullOrWhiteSpace(request.Type) ? "group" : request.Type.Trim(),
            TenantId = request.TenantId
        };
        _db.IdentityGroups.Add(group);
        await _db.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = group.Id }, new GroupDto
        {
            Id = group.Id, Name = group.Name, Type = group.Type
        });
    }

    [HttpPost("{id}/users/{userId}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> AddUser(string id, string userId, CancellationToken cancellationToken)
    {
        var group = await _db.IdentityGroups.SingleOrDefaultAsync(group => group.Id == id, cancellationToken);
        var user = await _db.Users.SingleOrDefaultAsync(user => user.Id == userId, cancellationToken);
        if (group is null || user is null || (group.TenantId is not null && user.TenantId != group.TenantId))
            return NotFound();

        _db.IdentityGroupMemberships.Add(new IdentityGroupMembershipRecord
        {
            GroupId = id, UserId = userId, TenantId = group.TenantId
        });
        await _db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id}/users/{userId}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> RemoveUser(string id, string userId, CancellationToken cancellationToken)
    {
        var membership = await _db.IdentityGroupMemberships.FindAsync([id, userId], cancellationToken);
        if (membership is null) return NotFound();
        _db.IdentityGroupMemberships.Remove(membership);
        await _db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    public class GroupDto
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
    }

    public sealed class GroupWriteDto
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = "group";
        public string? TenantId { get; set; }
    }
}
