using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using VertexBPMN.Api.Features;
using VertexBPMN.Api.Migration;

namespace VertexBPMN.Api.Controllers;

/// <summary>
/// Live Process Migration Controller
/// Live process migration API. It fails closed unless the qualified feature is explicitly enabled.
/// </summary>
[ApiController]
[Route("api/migration")]
public class MigrationController : ControllerBase
{
    private readonly ILiveProcessMigrationService _migrationService;
    private readonly ILogger<MigrationController> _logger;
    private readonly AdvancedFeatureOptions _features;

    public MigrationController(
        ILiveProcessMigrationService migrationService,
        ILogger<MigrationController> logger,
        IOptions<AdvancedFeatureOptions> features)
    {
        _migrationService = migrationService;
        _logger = logger;
        _features = features.Value;
    }

    /// <summary>
    /// Create a migration plan for moving instances from one process version to another
    /// </summary>
    [HttpPost("plan")]
    [Authorize(Policy = "ProcessManager")]
    public async Task<ActionResult<MigrationPlan>> CreateMigrationPlan([FromBody] CreateMigrationPlanRequest request)
    {
        if (!_features.LiveProcessMigration) return MigrationUnavailable();
        try
        {
            var plan = await _migrationService.CreateMigrationPlanAsync(
                request.FromProcessKey, 
                request.ToProcessKey, 
                request.Options);
            return Ok(plan);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating migration plan from {FromProcess} to {ToProcess}", 
                request.FromProcessKey, request.ToProcessKey);
            return StatusCode(500, new { error = "Failed to create migration plan" });
        }
    }

    /// <summary>
    /// Execute a migration plan
    /// </summary>
    [HttpPost("execute/{migrationPlanId}")]
    [Authorize(Policy = "ProcessManager")]
    public async Task<ActionResult<MigrationExecution>> ExecuteMigration(Guid migrationPlanId, [FromQuery] bool dryRun = false)
    {
        if (!_features.LiveProcessMigration) return MigrationUnavailable();
        try
        {
            var execution = await _migrationService.ExecuteMigrationAsync(migrationPlanId, dryRun);
            return Ok(execution);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing migration plan {PlanId}", migrationPlanId);
            return StatusCode(500, new { error = "Failed to execute migration" });
        }
    }

    /// <summary>
    /// Get migration status
    /// </summary>
    [HttpGet("status/{migrationId}")]
    public async Task<ActionResult<MigrationStatus>> GetMigrationStatus(Guid migrationId)
    {
        if (!_features.LiveProcessMigration) return MigrationUnavailable();
        try
        {
            var status = await _migrationService.GetMigrationStatusAsync(migrationId);
            return Ok(new { migrationId, status });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting migration status for {MigrationId}", migrationId);
            return StatusCode(500, new { error = "Failed to get migration status" });
        }
    }

    /// <summary>
    /// Rollback a migration
    /// </summary>
    [HttpPost("rollback/{migrationId}")]
    [Authorize(Policy = "ProcessManager")]
    public async Task<ActionResult> RollbackMigration(Guid migrationId)
    {
        if (!_features.LiveProcessMigration) return MigrationUnavailable();
        try
        {
            var success = await _migrationService.RollbackMigrationAsync(migrationId);
            if (success)
            {
                return Ok(new { message = "Migration rollback initiated successfully" });
            }
            return BadRequest(new { error = "Failed to initiate migration rollback" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rolling back migration {MigrationId}", migrationId);
            return StatusCode(500, new { error = "Failed to rollback migration" });
        }
    }

    /// <summary>
    /// Validate compatibility between two process versions
    /// </summary>
    [HttpGet("validate-compatibility")]
    public async Task<ActionResult<List<MigrationCompatibilityIssue>>> ValidateCompatibility(
        [FromQuery] string fromProcessKey, 
        [FromQuery] string toProcessKey)
    {
        if (!_features.LiveProcessMigration) return MigrationUnavailable();
        try
        {
            var issues = await _migrationService.ValidateCompatibilityAsync(fromProcessKey, toProcessKey);
            return Ok(issues);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating compatibility between {FromProcess} and {ToProcess}", 
                fromProcessKey, toProcessKey);
            return StatusCode(500, new { error = "Failed to validate compatibility" });
        }
    }

    /// <summary>
    /// Create a process instance snapshot for safe migration
    /// </summary>
    [HttpPost("snapshot/{processInstanceId}")]
    [Authorize(Policy = "ProcessManager")]
    public async Task<ActionResult<LiveMigrationSnapshot>> CreateSnapshot(Guid processInstanceId)
    {
        if (!_features.LiveProcessMigration) return MigrationUnavailable();
        try
        {
            var snapshot = await _migrationService.CreateSnapshotAsync(processInstanceId);
            return Ok(snapshot);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating snapshot for process instance {ProcessInstanceId}", processInstanceId);
            return StatusCode(500, new { error = "Failed to create process snapshot" });
        }
    }

    /// <summary>
    /// Restore process instance from snapshot
    /// </summary>
    [HttpPost("restore/{processInstanceId}/{snapshotId}")]
    [Authorize(Policy = "ProcessManager")]
    public async Task<ActionResult> RestoreFromSnapshot(Guid processInstanceId, Guid snapshotId)
    {
        if (!_features.LiveProcessMigration) return MigrationUnavailable();
        try
        {
            var success = await _migrationService.RestoreFromSnapshotAsync(processInstanceId, snapshotId);
            if (success)
            {
                return Ok(new { message = "Process instance restored successfully from snapshot" });
            }
            return BadRequest(new { error = "Failed to restore process instance from snapshot" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error restoring process instance {ProcessInstanceId} from snapshot {SnapshotId}", 
                processInstanceId, snapshotId);
            return StatusCode(500, new { error = "Failed to restore from snapshot" });
        }
    }

    private ObjectResult MigrationUnavailable() => Problem(
        statusCode: StatusCodes.Status501NotImplemented,
        title: "Live process migration is not qualified for production use",
        detail: "The current in-memory migration implementation is disabled until plans, snapshots, execution state, rollback, and audit are transactionally persisted.");
}

public class CreateMigrationPlanRequest
{
    public string FromProcessKey { get; set; } = string.Empty;
    public string ToProcessKey { get; set; } = string.Empty;
    public MigrationOptions Options { get; set; } = new();
}
