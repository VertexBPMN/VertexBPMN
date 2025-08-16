using Microsoft.AspNetCore.Mvc;
using VertexBPMN.Core.Services;
using System.Collections.Generic;

namespace VertexBPMN.Api.Controllers;

using Microsoft.AspNetCore.Authorization;
[ApiController]
[Route("api/vertex/authorization")]
[Authorize]
public class VertexAuthorizationController : ControllerBase
{
    private readonly IIdentityService _identityService;

    public VertexAuthorizationController(IIdentityService identityService)
    {
        _identityService = identityService;
    }

    [HttpGet]
    public async IAsyncEnumerable<AuthorizationDto> GetAll()
    {
        await foreach (var auth in _identityService.ListAuthorizationsAsync())
            yield return new AuthorizationDto { Id = auth.Id, UserId = auth.UserId, GroupId = auth.GroupId, Resource = auth.Resource, Permissions = auth.Permissions };
    }

    public class AuthorizationDto
    {
        public string Id { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string GroupId { get; set; } = string.Empty;
        public string Resource { get; set; } = string.Empty;
        public string Permissions { get; set; } = string.Empty;
    }
}
