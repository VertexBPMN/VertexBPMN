using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.Api.Controllers;

[ApiController]
[Route("api/runtime")]
[Authorize]
public class RuntimeController : ControllerBase
{
    private readonly IRuntimeService _runtimeService;

    public RuntimeController(IRuntimeService runtimeService)
    {
        _runtimeService = runtimeService;
    }

    /// <summary>
    /// Starts a new process instance by process definition key.
    /// </summary>
    /// <remarks>
    /// Example request:
    ///
    ///     POST /api/runtime/start
    ///     {
    ///         "ProcessDefinitionKey": "Process_HelloWorld",
    ///         "Variables": { "foo": 42 },
    ///         "BusinessKey": null,
    ///         "TenantId": null
    ///     }
    /// </remarks>
    /// <param name="request">Start request</param>
    /// <returns>The started process instance</returns>
    [HttpPost("start")]
    [ProducesResponseType(typeof(ProcessInstance), 201)]
    public async Task<ActionResult<ProcessInstance>> Start([FromBody] StartRequest request)
    {
        var tenantId = ResolveTenantId(request.TenantId);
        if (tenantId is null && !User.IsInRole("Admin")) return Forbid();
        var instance = await _runtimeService.StartProcessByKeyAsync(request.ProcessDefinitionKey, request.Variables, request.BusinessKey, tenantId);
        return CreatedAtAction(nameof(GetById), new { id = instance.Id }, instance);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ProcessInstance>> GetById(Guid id)
    {
        var instance = await _runtimeService.GetByIdAsync(id);
        if (instance is null) return NotFound();
        if (!CanAccessTenant(instance.TenantId)) return Forbid();
        return instance;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ProcessInstance>>> List(
        [FromQuery] Guid? processDefinitionId = null,
        [FromQuery] string? tenantId = null)
    {
        var effectiveTenantId = ResolveTenantId(tenantId);
        if (effectiveTenantId is null && !User.IsInRole("Admin")) return Forbid();
        var instances = new List<ProcessInstance>();
        await foreach (var instance in _runtimeService.ListAsync(processDefinitionId, effectiveTenantId))
            instances.Add(instance);
        return instances;
    }

    private string? ResolveTenantId(string? requestedTenantId) =>
        User.IsInRole("Admin")
            ? (string.IsNullOrWhiteSpace(requestedTenantId) ? null : requestedTenantId.Trim())
            : User.FindFirstValue("tenant_id");

    private bool CanAccessTenant(string? tenantId) =>
        User.IsInRole("Admin") || string.Equals(User.FindFirstValue("tenant_id"), tenantId, StringComparison.Ordinal);

    public record StartRequest(string ProcessDefinitionKey, IDictionary<string, object>? Variables, string? BusinessKey, string? TenantId);
}
