using Microsoft.AspNetCore.Mvc;
using VertexBPMN.Api.Dto;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.Api.Controllers;

using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
[ApiController]
[Route("api/vertex/deployment")]
[Authorize]
public class VertexDeploymentController : ControllerBase
{
    private readonly IRepositoryService _repositoryService;

    public VertexDeploymentController(IRepositoryService repositoryService)
    {
        _repositoryService = repositoryService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DeploymentDto>>> GetAll([FromQuery] string? tenantId = null)
    {
        var effectiveTenantId = ResolveTenantId(tenantId);
        if (effectiveTenantId is null && !User.IsInRole("Admin")) return Forbid();
        var deployments = new List<DeploymentDto>();
        await foreach (var def in _repositoryService.ListAsync(null, effectiveTenantId))
            deployments.Add(new DeploymentDto
            {
                Id = def.Id.ToString(),
                Name = def.Name,
                DeploymentTime = def.CreatedAt,
                TenantId = def.TenantId ?? string.Empty
            });
        return deployments;
    }

    private string? ResolveTenantId(string? requestedTenantId) =>
        User.IsInRole("Admin")
            ? (string.IsNullOrWhiteSpace(requestedTenantId) ? null : requestedTenantId.Trim())
            : User.FindFirstValue("tenant_id");
}
