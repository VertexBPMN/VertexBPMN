using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VertexBPMN.Application;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.Api.Controllers;

[ApiController]
[Route("api/triggers")]
[Authorize]
public sealed class WorkflowTriggerController(IWorkflowTriggerService triggerService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<WorkflowTriggerInfo>>> List(
        [FromQuery] string? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        var effectiveTenantId = ResolveTenantId(tenantId);
        if (effectiveTenantId is null && !User.IsInRole("Admin")) return Forbid();
        return Ok(await triggerService.ListAsync(effectiveTenantId, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<WorkflowTriggerInfo>> Get(
        Guid id,
        [FromQuery] string? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        var effectiveTenantId = ResolveTenantId(tenantId);
        if (effectiveTenantId is null && !User.IsInRole("Admin")) return Forbid();
        var trigger = await triggerService.GetAsync(id, effectiveTenantId, cancellationToken);
        return trigger is null ? NotFound() : Ok(trigger);
    }

    [HttpPost]
    [Authorize(Policy = "ProcessManager")]
    [ProducesResponseType(typeof(WorkflowTriggerCreated), StatusCodes.Status201Created)]
    public async Task<ActionResult<WorkflowTriggerCreated>> Create(
        [FromBody] CreateWorkflowTriggerRequest request,
        CancellationToken cancellationToken = default)
    {
        var effectiveTenantId = ResolveTenantId(request.TenantId);
        if (effectiveTenantId is null && !User.IsInRole("Admin")) return Forbid();
        try
        {
            var created = await triggerService.CreateAsync(
                request.Name, request.ProcessDefinitionKey, effectiveTenantId, cancellationToken);
            return CreatedAtAction(nameof(Get), new { id = created.Trigger.Id, tenantId = effectiveTenantId }, created);
        }
        catch (WorkflowTriggerProcessNotFoundException exception)
        {
            return NotFound(new ProblemDetails { Title = "Process definition not found", Detail = exception.Message });
        }
        catch (WorkflowTriggerConflictException exception)
        {
            return Conflict(new ProblemDetails { Title = "Trigger conflict", Detail = exception.Message });
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new ProblemDetails { Title = "Invalid trigger", Detail = exception.Message });
        }
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "ProcessManager")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateWorkflowTriggerRequest request,
        [FromQuery] string? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        var effectiveTenantId = ResolveTenantId(tenantId);
        if (effectiveTenantId is null && !User.IsInRole("Admin")) return Forbid();
        try
        {
            var updated = await triggerService.UpdateAsync(id, request.Name, request.Enabled, effectiveTenantId, cancellationToken);
            if (!updated) return NotFound();
            return NoContent();
        }
        catch (WorkflowTriggerConflictException exception)
        {
            return Conflict(new ProblemDetails { Title = "Trigger conflict", Detail = exception.Message });
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new ProblemDetails { Title = "Invalid trigger", Detail = exception.Message });
        }
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "ProcessManager")]
    public async Task<IActionResult> Delete(
        Guid id,
        [FromQuery] string? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        var effectiveTenantId = ResolveTenantId(tenantId);
        if (effectiveTenantId is null && !User.IsInRole("Admin")) return Forbid();
        return await triggerService.DeleteAsync(id, effectiveTenantId, cancellationToken)
            ? NoContent()
            : NotFound();
    }

    /// <summary>
    /// Invokes a trigger with its one-time-issued secret. The secret must be sent in
    /// X-VertexBPMN-Trigger-Secret and is never accepted in the request body.
    /// </summary>
    [HttpPost("{id:guid}/invoke")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ProcessInstance), StatusCodes.Status201Created)]
    public async Task<ActionResult<ProcessInstance>> Invoke(
        Guid id,
        [FromHeader(Name = "X-VertexBPMN-Trigger-Secret")] string? secret,
        [FromBody] InvokeWorkflowTriggerRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        var result = await triggerService.InvokeAsync(
            id,
            secret ?? string.Empty,
            request?.Variables,
            request?.BusinessKey,
            cancellationToken);
        return result.Status switch
        {
            WorkflowTriggerInvocationStatus.Started => Created($"/api/runtime/{result.ProcessInstance!.Id}", result.ProcessInstance),
            WorkflowTriggerInvocationStatus.InvalidSecret => Unauthorized(),
            WorkflowTriggerInvocationStatus.NotFound or WorkflowTriggerInvocationStatus.Disabled => NotFound(),
            WorkflowTriggerInvocationStatus.ProcessDefinitionNotFound => UnprocessableEntity(new ProblemDetails
            {
                Title = "Process definition not found",
                Detail = "The trigger points to a process definition that is no longer registered."
            }),
            _ => Problem("The workflow trigger could not be invoked.")
        };
    }

    private string? ResolveTenantId(string? requestedTenantId) =>
        User.IsInRole("Admin")
            ? (string.IsNullOrWhiteSpace(requestedTenantId) ? null : requestedTenantId.Trim())
            : User.FindFirstValue("tenant_id");

    public sealed record CreateWorkflowTriggerRequest(string Name, string ProcessDefinitionKey, string? TenantId);
    public sealed record UpdateWorkflowTriggerRequest(string? Name, bool? Enabled);
    public sealed record InvokeWorkflowTriggerRequest(IDictionary<string, object?>? Variables, string? BusinessKey);
}
