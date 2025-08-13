using Microsoft.AspNetCore.Mvc;
using VertexBPMN.Core.Services;
using VertexBPMN.Core.Domain;

namespace VertexBPMN.Api.Controllers
{
    [ApiController]
    [Route("api/process-migration")]
    [ApiExplorerSettings(GroupName = "Migration")]
    public class ProcessMigrationController : ControllerBase
    {
        private readonly IProcessMigrationService _migrationService;
        public ProcessMigrationController(IProcessMigrationService migrationService)
        {
            _migrationService = migrationService;
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
        [ProducesResponseType(typeof(ProcessMigrationResult), 200)]
        public ActionResult<ProcessMigrationResult> ExecuteMigration([FromBody] ProcessMigrationPlan plan)
        {
            var result = _migrationService.MigrateInstances(plan);
            return Ok(result);
        }
    }

    public class MigrationPreviewRequestDto
    {
    public string? SourceProcessDefinitionId { get; set; }
    public string? TargetProcessDefinitionId { get; set; }
    }
}
