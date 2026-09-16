using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VertexBPMN.Infrastructure.Persistence;

namespace VertexBPMN.Api.Controllers;

/// <summary>Tenant-scoped, payload-free external-task metadata for Studio and operators.</summary>
[ApiController]
[Route("api/external-task-operations")]
[Authorize(Policy = "TenantReadOnly")]
public sealed class ExternalTaskOperationsController(BpmnDbContext db, IConfiguration configuration) : ControllerBase
{
    [HttpGet("catalog")]
    public ActionResult<IReadOnlyList<ExternalTaskProfileDto>> Catalog([FromQuery] string? tenantId)
    {
        var tenant = ResolveTenant(tenantId, out var forbidden);
        if (forbidden) return Forbid();
        if (tenant is null) return BadRequest();
        if (!configuration.GetValue<bool>("ExternalTasks:Enabled")
            && !configuration.GetValue<bool>("ExternalTasks:EnableSchedulingPreview"))
            return Ok(Array.Empty<ExternalTaskProfileDto>());

        var entries = configuration.GetSection("ExternalTasks:Contracts").Get<ExternalTaskCatalogEntry[]>() ?? [];
        return Ok(entries
            .Where(entry => entry.Enabled
                && string.Equals(entry.TenantId, tenant, StringComparison.Ordinal)
                && IsValidAgentCatalogEntry(entry))
            .GroupBy(entry => (entry.AgentProfileRef, entry.Topic))
            .Where(group => group.Count() == 1)
            .Select(group => group.Single())
            .Select(entry => new ExternalTaskProfileDto(
                entry.AgentProfileRef!, entry.AgentProfileVersion ?? entry.Version, entry.Topic,
                entry.MaxAttempts, entry.MaxDeadlineSeconds,
                entry.Inputs.Select(field => new ExternalTaskFieldDto(field.Name, field.Type, field.Required, field.MaxLength)).ToArray(),
                entry.Outputs.Select(field => new ExternalTaskFieldDto(field.Name, field.Type, field.Required, field.MaxLength)).ToArray()))
            .OrderBy(entry => entry.ProfileRef, StringComparer.Ordinal)
            .ToArray());
    }

    private static bool IsValidAgentCatalogEntry(ExternalTaskCatalogEntry entry)
    {
        static bool ValidFields(ExternalTaskScalarField[]? fields) => fields is not null
            && fields.Length <= 32
            && fields.Select(field => field.Name).Distinct(StringComparer.Ordinal).Count() == fields.Length
            && fields.All(field => !string.IsNullOrWhiteSpace(field.Name) && field.Name.Length <= 128
                && field.Name.All(character => char.IsAsciiLetterOrDigit(character) || character == '_')
                && field.Type is "string" or "boolean" or "integer"
                && field.AllowExternalTransfer && field.MaxLength is >= 1 and <= 65536);

        return !string.IsNullOrWhiteSpace(entry.AgentProfileRef)
            && !string.IsNullOrWhiteSpace(entry.AgentProfileVersion)
            && entry.Topic.StartsWith("agent.", StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(entry.Version)
            && entry.MaxAttempts is >= 1 and <= 10
            && entry.MaxDeadlineSeconds is >= 1 and <= 86400
            && ValidFields(entry.Inputs) && ValidFields(entry.Outputs);
    }

    [HttpGet("process/{processInstanceId:guid}")]
    public async Task<ActionResult<IReadOnlyList<ExternalTaskOperationDto>>> Process(
        Guid processInstanceId,
        [FromQuery] string? tenantId,
        [FromQuery] string? activityId,
        CancellationToken cancellationToken)
    {
        var tenant = ResolveTenant(tenantId, out var forbidden);
        if (forbidden) return Forbid();
        if (tenant is null) return BadRequest();

        var processExists = await db.ProcessInstances.AsNoTracking().AnyAsync(instance =>
            instance.Id == processInstanceId && instance.TenantId == tenant, cancellationToken);
        if (!processExists) return NotFound();

        var query = db.ExternalTaskJobs.AsNoTracking().Where(job =>
            job.TenantId == tenant && job.ProcessInstanceId == processInstanceId);
        if (!string.IsNullOrWhiteSpace(activityId))
        {
            var exactActivityId = activityId.Trim();
            query = query.Where(job => job.ActivityId == exactActivityId);
        }

        return Ok(await query.OrderByDescending(job => job.CreatedAt).Select(job => new ExternalTaskOperationDto(
            job.Id, job.ProcessInstanceId, job.ActivityId, job.Topic, job.AgentProfileRef,
            job.AgentProfileVersion, job.State.ToString(), job.AttemptsStarted, job.MaxAttempts,
            job.CreatedAt, job.AvailableAt, job.Deadline, job.LeaseExpiresAt, job.ErrorCode)).ToArrayAsync(cancellationToken));
    }

    private string? ResolveTenant(string? requested, out bool forbidden)
    {
        var requestedTenant = string.IsNullOrWhiteSpace(requested) ? null : requested.Trim();
        var claimTenant = User.FindFirstValue("tenant_id");
        forbidden = !User.IsInRole("Admin") && requestedTenant is not null
            && !string.Equals(requestedTenant, claimTenant, StringComparison.Ordinal);
        return User.IsInRole("Admin") ? requestedTenant ?? claimTenant : claimTenant;
    }

    public sealed record ExternalTaskFieldDto(string Name, string Type, bool Required, int MaxLength);
    public sealed record ExternalTaskProfileDto(
        string ProfileRef, string ProfileVersion, string Topic, int MaxAttempts, int MaxDeadlineSeconds,
        IReadOnlyList<ExternalTaskFieldDto> Inputs, IReadOnlyList<ExternalTaskFieldDto> Outputs);
    public sealed record ExternalTaskOperationDto(
        Guid JobId, Guid ProcessInstanceId, string ActivityId, string Topic, string? AgentProfileRef,
        string? AgentProfileVersion, string State, int AttemptsStarted, int MaxAttempts,
        long CreatedAt, long AvailableAt, long Deadline, long? LeaseExpiresAt, string? ErrorCode);
}
