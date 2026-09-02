using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using VertexBPMN.Api.Features;
using VertexBPMN.Api.Migration;
using VertexBPMN.Domain.Entities;

namespace VertexBPMN.Api.Controllers;

/// <summary>
/// Compatibility facade for Studio clients using the original migration route.
/// Every operation delegates to the persistent, transactional migration engine.
/// </summary>
[ApiController]
[Route("api/process-migration")]
[ApiExplorerSettings(GroupName = "Migration")]
[Obsolete("Use /api/migration for new integrations.")]
public sealed class ProcessMigrationController : ControllerBase
{
    private readonly ILiveProcessMigrationService _migrationService;
    private readonly AdvancedFeatureOptions _features;

    public ProcessMigrationController(
        ILiveProcessMigrationService migrationService,
        IOptions<AdvancedFeatureOptions> features)
    {
        _migrationService = migrationService;
        _features = features.Value;
    }

    [HttpPost("plan/feedback")]
    [Authorize(Policy = "ProcessManager")]
    public async Task<ActionResult<ProcessMigrationResult>> GetMigrationFeedback([FromBody] ProcessMigrationPlan request)
    {
        if (!_features.LiveProcessMigration) return MigrationUnavailable();
        try
        {
            var plan = await ResolvePlanAsync(request);
            var execution = await _migrationService.ExecuteMigrationAsync(
                plan.Id, dryRun: true, tenantId: ResolveTenant());
            return Ok(ToLegacyResult(execution, plan));
        }
        catch (Exception ex) when (ex is FormatException or InvalidOperationException or ArgumentException)
        {
            return BadRequest(new ProblemDetails { Detail = ex.Message });
        }
    }

    [HttpPost("plan/preview")]
    [Authorize(Policy = "ProcessManager")]
    public async Task<ActionResult<ProcessMigrationPlan>> PreviewMigration([FromBody] MigrationPreviewRequestDto request)
    {
        if (!_features.LiveProcessMigration) return MigrationUnavailable();
        if (!Guid.TryParse(request.SourceProcessDefinitionId, out var sourceId)
            || !Guid.TryParse(request.TargetProcessDefinitionId, out var targetId))
            return BadRequest(new ProblemDetails { Detail = "Valid source and target process definition IDs are required." });

        try
        {
            var plan = await _migrationService.CreateMigrationPlanByDefinitionIdAsync(
                sourceId, targetId, new MigrationOptions(), ResolveTenant());
            return Ok(ToLegacyPlan(plan));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ProblemDetails { Detail = ex.Message });
        }
    }

    [HttpPost("plan/execute")]
    [Authorize(Policy = "ProcessManager")]
    public async Task<ActionResult<ProcessMigrationResult>> ExecuteMigration([FromBody] ProcessMigrationPlan request)
    {
        if (!_features.LiveProcessMigration) return MigrationUnavailable();
        try
        {
            var plan = await ResolvePlanAsync(request);
            var execution = await _migrationService.ExecuteMigrationAsync(
                plan.Id, dryRun: false, tenantId: ResolveTenant());
            return Ok(ToLegacyResult(execution, plan));
        }
        catch (Exception ex) when (ex is FormatException or InvalidOperationException or ArgumentException)
        {
            return BadRequest(new ProblemDetails { Detail = ex.Message });
        }
    }

    private async Task<MigrationPlan> ResolvePlanAsync(ProcessMigrationPlan request)
    {
        if (request.QualifiedPlanId is { } persistedPlanId)
        {
            return new MigrationPlan
            {
                Id = persistedPlanId,
                FromProcessDefinitionId = ParseDefinitionId(request.SourceProcessDefinitionId, "source"),
                ToProcessDefinitionId = ParseDefinitionId(request.TargetProcessDefinitionId, "target")
            };
        }

        return await _migrationService.CreateMigrationPlanByDefinitionIdAsync(
            ParseDefinitionId(request.SourceProcessDefinitionId, "source"),
            ParseDefinitionId(request.TargetProcessDefinitionId, "target"),
            new MigrationOptions(),
            ResolveTenant());
    }

    private static Guid ParseDefinitionId(string value, string name) =>
        Guid.TryParse(value, out var id)
            ? id
            : throw new FormatException($"A valid {name} process definition ID is required.");

    private static ProcessMigrationPlan ToLegacyPlan(MigrationPlan plan) => new()
    {
        SourceProcessDefinitionId = plan.FromProcessDefinitionId.ToString(),
        TargetProcessDefinitionId = plan.ToProcessDefinitionId.ToString(),
        QualifiedPlanId = plan.Id,
        ActivityMappings = plan.ActivityMappings
            .Where(mapping => !string.IsNullOrWhiteSpace(mapping.ToActivityId))
            .ToDictionary(mapping => mapping.FromActivityId, mapping => mapping.ToActivityId, StringComparer.Ordinal)
    };

    private static ProcessMigrationResult ToLegacyResult(MigrationExecution execution, MigrationPlan plan) => new()
    {
        Success = execution.Status == MigrationStatus.Completed,
        MigratedInstanceIds = execution.AffectedProcessInstanceIds.Select(id => id.ToString()).ToList(),
        Errors = execution.Error is null ? [] : [execution.Error],
        Warnings = plan.CompatibilityIssues.Select(issue => issue.Description).ToList()
    };

    private string? ResolveTenant() =>
        User.IsInRole("Admin") ? null : User.FindFirstValue("tenant_id");

    private ObjectResult MigrationUnavailable() => Problem(
        statusCode: StatusCodes.Status501NotImplemented,
        title: "Live process migration is disabled",
        detail: "Set AdvancedFeatures:LiveProcessMigration=true to enable the qualified transactional migration API.");
}

public sealed class MigrationPreviewRequestDto
{
    public string? SourceProcessDefinitionId { get; set; }
    public string? TargetProcessDefinitionId { get; set; }
}
