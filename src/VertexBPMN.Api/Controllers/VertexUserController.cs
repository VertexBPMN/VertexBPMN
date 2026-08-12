using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Infrastructure.Persistence;

namespace VertexBPMN.Api.Controllers;

using Microsoft.AspNetCore.Authorization;
[ApiController]
[Route("api/vertex/user")]
[Authorize]
public class VertexUserController : ControllerBase
{
    private readonly IIdentityService _identityService;
    private readonly BpmnDbContext _db;

    public VertexUserController(IIdentityService identityService, BpmnDbContext db)
    {
        _identityService = identityService;
        _db = db;
    }

    [HttpGet]
    public async IAsyncEnumerable<UserDto> GetAll()
    {
        var tenantId = User.IsInRole("Admin") ? null : User.FindFirst("tenant_id")?.Value;
        await foreach (var user in _identityService.ListUsersAsync(tenantId: tenantId))
            yield return new UserDto { Id = user.Id, Username = user.Username, Email = user.Email };
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<UserDto>> GetById(string id)
    {
        var tenantId = User.IsInRole("Admin") ? null : User.FindFirst("tenant_id")?.Value;
        var user = await _identityService.GetUserByIdAsync(id, tenantId: tenantId);
        if (user is null) return NotFound();
        return new UserDto { Id = user.Id, Username = user.Username, Email = user.Email };
    }

    [HttpPost]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<UserDto>> Create([FromBody] UserWriteDto request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Email))
            return ValidationProblem("Username and email are required.");

        if (await _db.Users.AnyAsync(user => user.Username == request.Username, cancellationToken))
            return Conflict(new ProblemDetails { Title = "Username already exists" });

        var user = new User
        {
            Username = request.Username.Trim(),
            Email = request.Email.Trim(),
            TenantId = request.TenantId,
            Roles = request.Roles ?? []
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = user.Id }, ToDto(user));
    }

    [HttpPut("{id}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<UserDto>> Update(string id, [FromBody] UserWriteDto request, CancellationToken cancellationToken)
    {
        var user = await _db.Users.SingleOrDefaultAsync(user => user.Id == id, cancellationToken);
        if (user is null) return NotFound();
        user.Username = request.Username.Trim();
        user.Email = request.Email.Trim();
        user.TenantId = request.TenantId;
        user.Roles = request.Roles ?? [];
        user.LastModified = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return Ok(ToDto(user));
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Delete(string id, CancellationToken cancellationToken)
    {
        var user = await _db.Users.SingleOrDefaultAsync(user => user.Id == id, cancellationToken);
        if (user is null) return NotFound();
        _db.Users.Remove(user);
        await _db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private static UserDto ToDto(User user) => new() { Id = user.Id, Username = user.Username, Email = user.Email };

    public class UserDto
    {
        public string Id { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
    }

    public sealed class UserWriteDto
    {
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? TenantId { get; set; }
        public List<string>? Roles { get; set; }
    }
}
