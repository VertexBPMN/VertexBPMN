using Microsoft.AspNetCore.Mvc;
using VertexBPMN.Api.Migration;

namespace VertexBPMN.Api.Controllers;

/// <summary>
/// Live Process Migration Controller
/// Olympic-level feature: Innovation Differentiators - Live Migration
/// </summary>
[ApiController]
[Route("api/migration")]
public class MigrationController : ControllerBase
{
    private readonly ILiveProcessMigrationService _migrationService;
    private readonly ILogger<MigrationController> _logger;

    public MigrationController(
        ILiveProcessMigrationService migrationService,
        ILogger<MigrationController> logger)
    {
        _migrationService = migrationService;
        _logger = logger;
    }

    /// <summary>
    /// Create a migration plan for moving instances from one process version to another
    /// </summary>
    [HttpPost("plan")]
    public async Task<ActionResult<MigrationPlan>> CreateMigrationPlan([FromBody] CreateMigrationPlanRequest request)
    {
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
    public async Task<ActionResult<MigrationExecution>> ExecuteMigration(Guid migrationPlanId, [FromQuery] bool dryRun = false)
    {
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
    public async Task<ActionResult> RollbackMigration(Guid migrationId)
    {
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
    public async Task<ActionResult<LiveMigrationSnapshot>> CreateSnapshot(Guid processInstanceId)
    {
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
    public async Task<ActionResult> RestoreFromSnapshot(Guid processInstanceId, Guid snapshotId)
    {
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
}

public class CreateMigrationPlanRequest
{
    public string FromProcessKey { get; set; } = string.Empty;
    public string ToProcessKey { get; set; } = string.Empty;
    public MigrationOptions Options { get; set; } = new();
}
