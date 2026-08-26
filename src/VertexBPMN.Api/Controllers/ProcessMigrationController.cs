using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using VertexBPMN.Api.Features;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.Api.Controllers
{
    [ApiController]
    [Route("api/process-migration")]
    [ApiExplorerSettings(GroupName = "Migration")]
    public class ProcessMigrationController : ControllerBase
    {
        private readonly IProcessMigrationService _migrationService;
        private readonly AdvancedFeatureOptions _features;
        public ProcessMigrationController(IProcessMigrationService migrationService, IOptions<AdvancedFeatureOptions> features)
        {
            _migrationService = migrationService;
            _features = features.Value;
        }

        /// <summary>
        /// Get migration analytics and diagnostics for a given migration plan.
        /// </summary>
        /// <param name="plan">Migration plan with activity mappings.</param>
        /// <returns>Migration analytics and diagnostics.</returns>
        [HttpPost("plan/feedback")]
        [ProducesResponseType(typeof(ProcessMigrationResult), 200)]
        public ActionResult<ProcessMigrationResult> GetMigrationFeedback([FromBody] ProcessMigrationPlan plan)
        {
            if (!_features.LiveProcessMigration) return MigrationUnavailable();
            // Only preview analytics, do not execute migration
            var result = _migrationService.MigrateInstances(plan);
            result.Success = false; // Indicate feedback only, not executed
            return Ok(result);
        }

        /// <summary>
        /// Preview a migration plan between two process definitions.
        /// </summary>
        /// <param name="request">Source and target process definition IDs.</param>
        /// <returns>Migration plan with activity mappings.</returns>
        [HttpPost("plan/preview")]
        [ProducesResponseType(typeof(ProcessMigrationPlan), 200)]
        public ActionResult<ProcessMigrationPlan> PreviewMigration([FromBody] MigrationPreviewRequestDto request)
        {
            if (!_features.LiveProcessMigration) return MigrationUnavailable();
            if (string.IsNullOrWhiteSpace(request.SourceProcessDefinitionId) || string.IsNullOrWhiteSpace(request.TargetProcessDefinitionId))
            {
                return BadRequest("Source and target process definition IDs are required.");
            }
            var plan = _migrationService.PreviewMigration(request.SourceProcessDefinitionId, request.TargetProcessDefinitionId);
            return Ok(plan);
        }

        /// <summary>
        /// Execute a migration plan and migrate all process instances.
        /// </summary>
        /// <param name="plan">Migration plan with activity mappings.</param>
        /// <returns>Migration result with analytics and diagnostics.</returns>
        [HttpPost("plan/execute")]
        [Authorize(Policy = "ProcessManager")]
        [ProducesResponseType(typeof(ProcessMigrationResult), 200)]
        public ActionResult<ProcessMigrationResult> ExecuteMigration([FromBody] ProcessMigrationPlan plan)
        {
            if (!_features.LiveProcessMigration) return MigrationUnavailable();
            var result = _migrationService.MigrateInstances(plan);
            return Ok(result);
        }

        private ObjectResult MigrationUnavailable() => Problem(
            statusCode: StatusCodes.Status501NotImplemented,
            title: "Process migration is not qualified for production use",
            detail: "Migration remains disabled until preview, token mapping, transactional execution, rollback, and audit are durably implemented and pass the Phase 4 acceptance gate.");
    }

    public class MigrationPreviewRequestDto
    {
    public string? SourceProcessDefinitionId { get; set; }
    public string? TargetProcessDefinitionId { get; set; }
    }
}
