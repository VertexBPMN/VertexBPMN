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
        var liveInspectorEnabled = await _db.FeatureFlags.AsNoTracking()
            .Where(flag => flag.Name == "liveinspector")
            .Select(flag => (bool?)flag.Enabled)
            .SingleOrDefaultAsync(cancellationToken) ?? true;
        if (!liveInspectorEnabled)
            return StatusCode(503, "Live Inspector feature is disabled.");

        var instance = await _runtimeService.GetByIdAsync(id, cancellationToken);
        if (instance is null)
            return NotFound();

        var tenantId = User.FindFirstValue("tenant_id");
        if (!string.IsNullOrWhiteSpace(instance.TenantId) &&
            !string.Equals(instance.TenantId, tenantId, StringComparison.Ordinal))
            return Forbid();

        var repositoryService = HttpContext.RequestServices.GetService<IRepositoryService>();
        var bpmnXml = string.Empty;
        if (repositoryService is not null && instance.ProcessDefinitionId != Guid.Empty)
        {
            var definition = await repositoryService.GetByIdAsync(instance.ProcessDefinitionId, cancellationToken);
            bpmnXml = definition?.BpmnXml ?? string.Empty;
        }

        var tokenRepository = HttpContext.RequestServices.GetService<IExecutionTokenRepository>();
        var tokens = new List<ExecutionToken>();
        if (tokenRepository is not null)
        {
            await foreach (var token in tokenRepository.ListByProcessInstanceAsync(id, cancellationToken))
                tokens.Add(token);
        }

        var variableRepository = HttpContext.RequestServices.GetService<IVariableRepository>();
        var variables = new List<Variable>();
        if (variableRepository is not null)
        {
            await foreach (var variable in variableRepository.ListByScopeAsync(id, cancellationToken))
                variables.Add(variable);
        }

        var multiInstanceRepository = HttpContext.RequestServices.GetService<IMultiInstanceExecutionRepository>();
        var multiInstances = new List<MultiInstanceExecution>();
        if (multiInstanceRepository is not null)
        {
            await foreach (var multiInstance in multiInstanceRepository.ListByProcessInstanceAsync(id, cancellationToken))
                multiInstances.Add(multiInstance);
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
}
