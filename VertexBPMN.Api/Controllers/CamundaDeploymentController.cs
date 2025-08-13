using Microsoft.AspNetCore.Mvc;
using VertexBPMN.Core.Services;
using VertexBPMN.Api.Dto;
using CoreDef = VertexBPMN.Core.Domain.ProcessDefinition;

namespace VertexBPMN.Api.Controllers;

using Microsoft.AspNetCore.Authorization;
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
    public async IAsyncEnumerable<DeploymentDto> GetAll([FromQuery] string? tenantId = null)
    {
        // Annahme: Deployment-Infos werden aus ProcessDefinitions aggregiert
        await foreach (var def in _repositoryService.ListAsync(null, tenantId))
            yield return new DeploymentDto
            {
                Id = def.Id.ToString(),
                Name = def.Name,
                DeploymentTime = def.CreatedAt,
                TenantId = def.TenantId ?? string.Empty
            };
    }
}
