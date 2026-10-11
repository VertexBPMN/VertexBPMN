using Microsoft.AspNetCore.Mvc;
using VertexBPMN.Api.Dto;
using VertexBPMN.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace VertexBPMN.Api.Controllers;
[ApiController]
[Route("api/vertex/deployment")]
[Authorize]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515", Justification = "Public controller required for ASP.NET Core MVC discovery and activation.")]
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
		if (effectiveTenantId is null && !User.IsInRole("Admin"))
		{
			return Forbid();
		}

		var deployments = new List<DeploymentDto>();
		await foreach (var def in _repositoryService.ListAsync(null, effectiveTenantId))
		{
			deployments.Add(new DeploymentDto { Id = def.Id.ToString(), Name = def.Name, DeploymentTime = def.CreatedAt, TenantId = def.TenantId ?? string.Empty });
		}

		return deployments;
	}

	private string? ResolveTenantId(string? requestedTenantId)
	{
		if (!User.IsInRole("Admin"))
		{
			return User.FindFirstValue("tenant_id");
		}

		return string.IsNullOrWhiteSpace(requestedTenantId) ? null : requestedTenantId.Trim();
	}
}
