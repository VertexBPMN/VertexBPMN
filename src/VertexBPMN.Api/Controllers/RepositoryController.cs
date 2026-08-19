using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.Api.Controllers;

[ApiController]
[Route("api/repository")]
[Authorize]
public class RepositoryController : ControllerBase
{
    private readonly IRepositoryService _repositoryService;
    private readonly IWorkflowTriggerService _workflowTriggerService;

    public RepositoryController(IRepositoryService repositoryService, IWorkflowTriggerService workflowTriggerService)
    {
        _repositoryService = repositoryService;
        _workflowTriggerService = workflowTriggerService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ProcessDefinition>>> GetAll(
        [FromQuery] string? key = null,
        [FromQuery] string? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        var effectiveTenantId = ResolveTenantId(tenantId);
        if (effectiveTenantId is null && !User.IsInRole("Admin")) return Forbid();

        var definitions = new List<ProcessDefinition>();
        await foreach (var definition in _repositoryService.ListAsync(key, effectiveTenantId, cancellationToken))
            definitions.Add(definition);
        return definitions;
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ProcessDefinition>> GetById(Guid id)
    {
        var def = await _repositoryService.GetByIdAsync(id);
        if (def is null) return NotFound();
        if (!CanAccessTenant(def.TenantId)) return Forbid();
        return def;
    }

    /// <summary>
    /// Deploys a new BPMN process definition.
    /// </summary>
    /// <remarks>
    /// Example request:
    ///
    ///     POST /api/repository
    ///     {
    ///         "bpmnXml": "&lt;definitions ...&gt;...&lt;/definitions&gt;",
    ///         "name": "hello-world.bpmn",
    ///         "tenantId": null
    ///     }
    /// </remarks>
    /// <param name="request">Deployment request</param>
    /// <returns>The deployed process definition</returns>
    [HttpPost]
    [Authorize(Policy = "ProcessManager")]
    [ProducesResponseType(typeof(ProcessDefinition), 201)]
    public async Task<ActionResult<ProcessDefinition>> Deploy([FromBody] RepositoryDeployRequest request)
    {
        var effectiveTenantId = ResolveTenantId(request.TenantId);
        if (effectiveTenantId is null && !User.IsInRole("Admin")) return Forbid();

        var def = await _repositoryService.DeployAsync(request.BpmnXml, request.Name, effectiveTenantId);
        await _workflowTriggerService.SynchronizeBpmnWebhooksAsync(request.BpmnXml, def.Key, effectiveTenantId);
        return CreatedAtAction(nameof(GetById), new { id = def.Id }, def);
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = "ProcessManager")]
    public async Task<IActionResult> Delete(Guid id, [FromQuery] string? tenantId = null)
    {
        var def = await _repositoryService.GetByIdAsync(id);
        if (def is null) return NotFound();
        if (!CanAccessTenant(def.TenantId, tenantId)) return Forbid();
        await _repositoryService.DeleteAsync(id);
        return NoContent();
    }

    private bool CanAccessTenant(string? tenantId, string? requestedTenantId = null) =>
        User.IsInRole("Admin")
            ? string.IsNullOrWhiteSpace(requestedTenantId) || string.Equals(requestedTenantId, tenantId, StringComparison.Ordinal)
            : string.Equals(User.FindFirstValue("tenant_id"), tenantId, StringComparison.Ordinal);

    private string? ResolveTenantId(string? requestedTenantId)
    {
        if (User.IsInRole("Admin"))
            return string.IsNullOrWhiteSpace(requestedTenantId) ? null : requestedTenantId.Trim();

        return User.FindFirstValue("tenant_id");
    }
    public record RepositoryDeployRequest(string BpmnXml, string Name, string? TenantId);
}
