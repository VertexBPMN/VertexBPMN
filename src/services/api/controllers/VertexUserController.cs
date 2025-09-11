using Microsoft.AspNetCore.Mvc;
using VertexBPMN.Core.Services;
using System.Collections.Generic;

namespace VertexBPMN.Api.Controllers;

using Microsoft.AspNetCore.Authorization;
[ApiController]
[Route("api/vertex/user")]
[Authorize]
public class VertexUserController : ControllerBase
{
    private readonly IIdentityService _identityService;

    public VertexUserController(IIdentityService identityService)
    {
        _identityService = identityService;
    }

    [HttpGet]
    public async IAsyncEnumerable<UserDto> GetAll()
    {
        await foreach (var user in _identityService.ListUsersAsync())
            yield return new UserDto { Id = user.Id, Username = user.Username, Email = user.Email };
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<UserDto>> GetById(string id)
    {
        var user = await _identityService.GetUserByIdAsync(id);
        if (user is null) return NotFound();
        return new UserDto { Id = user.Id, Username = user.Username, Email = user.Email };
    }

    public class UserDto
    {
        public string Id { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
    }
}
