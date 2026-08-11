using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Domain.Model.Dmn;

namespace VertexBPMN.Api.Controllers
{
    [ApiController]
    [Route("api/decision")]
    [Authorize]
    public class DecisionController : ControllerBase
    {
        private readonly IDecisionService _decisionService;

        public DecisionController(IDecisionService decisionService)
        {
            _decisionService = decisionService;
        }

        [HttpPost("deploy")]
        [Authorize(Policy = "ProcessManager")]
        public async Task<IActionResult> Deploy([FromBody] DeployRequest request)
        {
            var tenantId = ResolveTenantId(request.TenantId);
            if (tenantId is null && !User.IsInRole("Admin")) return Forbid();
            await _decisionService.DeployAsync(request.DecisionKey, request.Name, request.DmnXml, tenantId);
            return Ok();
        }

        public record DeployRequest(string DecisionKey, string Name, string DmnXml, string? TenantId = null);

        [HttpGet("by-key")]
        public async Task<ActionResult<DecisionDefinition>> GetDecisionByKey([FromQuery] string decisionKey, [FromQuery] string? tenantId = null)
        {
            var effectiveTenantId = ResolveTenantId(tenantId);
            if (effectiveTenantId is null && !User.IsInRole("Admin")) return Forbid();
            var def = await _decisionService.GetDecisionByKeyAsync(decisionKey, effectiveTenantId);
            if (def is null) return NotFound();
            return def;
        }

        /// <summary>
        /// Evaluates a DMN decision by key with input variables.
        /// </summary>
        /// <remarks>
        /// Example request:
        ///
        ///     POST /api/decision/evaluate
        ///     {
        ///         "DecisionKey": "my-decision",
        ///         "Inputs": { "input1": "value" }
        ///     }
        /// </remarks>
        /// <param name="request">Evaluation request</param>
        /// <returns>The DMN decision result</returns>
        [HttpPost("evaluate")]
        [ProducesResponseType(typeof(DecisionResult), 200)]
        public async Task<ActionResult<DecisionResult>> Evaluate([FromBody] EvaluateRequest request)
        {
            var tenantId = ResolveTenantId(request.TenantId);
            if (tenantId is null && !User.IsInRole("Admin")) return Forbid();
            var result = await _decisionService.EvaluateDecisionByKeyAsync(request.DecisionKey, request.Inputs, tenantId);
            return Ok(result);
        }

        public record EvaluateRequest(string DecisionKey, IDictionary<string, object> Inputs, string? TenantId = null);

        private string? ResolveTenantId(string? requestedTenantId) =>
            User.IsInRole("Admin")
                ? (string.IsNullOrWhiteSpace(requestedTenantId) ? null : requestedTenantId.Trim())
                : User.FindFirstValue("tenant_id");
    }
}
