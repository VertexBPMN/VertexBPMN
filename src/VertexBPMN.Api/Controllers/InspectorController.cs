using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Domain.Interfaces.Repositories;
using VertexBPMN.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace VertexBPMN.Api.Controllers;
[ApiController]
[Route("api/inspector")]
[Authorize]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515", Justification = "Public controller required for ASP.NET Core MVC discovery and activation.")]
public class InspectorController : ControllerBase
{
	private readonly IRuntimeService _runtimeService;
	private readonly BpmnDbContext _db;
	public InspectorController(IRuntimeService runtimeService, BpmnDbContext db)
	{
		_runtimeService = runtimeService;
		_db = db;
	}

	/// <summary>
	/// Returns the current state (active tokens, activities, variables) of a process instance for live inspection/visualization.
	/// </summary>
	[HttpGet("process-instance/{id}/state")]
	public async Task<IActionResult> GetProcessInstanceState(Guid id, CancellationToken cancellationToken)
	{
		var liveInspectorEnabled = await _db.FeatureFlags.AsNoTracking().Where(flag => flag.Name == "liveinspector").Select(flag => (bool? )flag.Enabled).SingleOrDefaultAsync(cancellationToken) ?? true;
		if (!liveInspectorEnabled)
		{
			return StatusCode(503, "Live Inspector feature is disabled.");
		}

		var instance = await _runtimeService.GetByIdAsync(id, cancellationToken);
		if (instance is null)
		{
			return NotFound();
		}

		var tenantId = User.FindFirstValue("tenant_id");
		if (!string.IsNullOrWhiteSpace(instance.TenantId) && !string.Equals(instance.TenantId, tenantId, StringComparison.Ordinal))
		{
			return Forbid();
		}

		var bpmnXml = await GetDefinitionXmlAsync(instance.ProcessDefinitionId, cancellationToken);

		var tokens = await GetTokensAsync(id, cancellationToken);

		var variableRepository = HttpContext.RequestServices.GetService<IVariableRepository>();
		var variables = new List<Variable>();
		if (variableRepository is not null)
		{
			await foreach (var variable in variableRepository.ListByScopeAsync(id, cancellationToken))
			{
				variables.Add(variable);
			}
		}

		var multiInstanceRepository = HttpContext.RequestServices.GetService<IMultiInstanceExecutionRepository>();
		var multiInstances = new List<MultiInstanceExecution>();
		if (multiInstanceRepository is not null)
		{
			await foreach (var multiInstance in multiInstanceRepository.ListByProcessInstanceAsync(id, cancellationToken))
			{
				multiInstances.Add(multiInstance);
			}
		}

		var state = new
		{
			Instance = instance,
			BpmnXml = bpmnXml,
			CurrentActivityId = tokens.FirstOrDefault()?.CurrentNodeId ?? string.Empty,
			Tokens = tokens,
			Variables = variables,
			MultiInstances = multiInstances
		};
		return Ok(state);
	}

	private async Task<List<ExecutionToken>> GetTokensAsync(Guid instanceId, CancellationToken cancellationToken)
	{
		var repository = HttpContext.RequestServices.GetService<IExecutionTokenRepository>();
		var tokens = new List<ExecutionToken>();
		if (repository is not null)
		{
			await foreach (var token in repository.ListByProcessInstanceAsync(instanceId, cancellationToken))
			{
				tokens.Add(token);
			}
		}

		return tokens;
	}

	private async Task<string> GetDefinitionXmlAsync(Guid definitionId, CancellationToken cancellationToken)
	{
		var repository = HttpContext.RequestServices.GetService<IRepositoryService>();
		if (definitionId == Guid.Empty || repository is null)
		{
			return string.Empty;
		}

		var definition = await repository.GetByIdAsync(definitionId, cancellationToken);
		return definition?.BpmnXml ?? string.Empty;
	}
}
