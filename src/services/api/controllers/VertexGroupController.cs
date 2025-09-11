using Microsoft.AspNetCore.Mvc;
using VertexBPMN.Core.Services;
using System.Collections.Generic;

namespace VertexBPMN.Api.Controllers;

using Microsoft.AspNetCore.Authorization;
[ApiController]
[Route("api/vertex/group")]
[Authorize]
public class VertexGroupController : ControllerBase
{
    private readonly IIdentityService _identityService;

    public VertexGroupController(IIdentityService identityService)
    {
        _identityService = identityService;
    }

    [HttpGet]
    public async IAsyncEnumerable<GroupDto> GetAll()
    {
        await foreach (var group in _identityService.ListGroupsAsync())
            yield return new GroupDto { Id = group.Id, Name = group.Name, Type = group.Type };
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<GroupDto>> GetById(string id)
    {
        var group = await _identityService.GetGroupByIdAsync(id);
        if (group is null) return NotFound();
        return new GroupDto { Id = group.Id, Name = group.Name, Type = group.Type };
    }

    public class GroupDto
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
    }
}
