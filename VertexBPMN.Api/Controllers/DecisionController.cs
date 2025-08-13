using Microsoft.AspNetCore.Mvc;
using VertexBPMN.Core.Services;

namespace VertexBPMN.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DecisionController : ControllerBase
    {
        private readonly IDecisionService _decisionService;

        public DecisionController(IDecisionService decisionService)
        {
            _decisionService = decisionService;
        }

        [HttpPost("deploy")]
        public async Task<IActionResult> Deploy([FromBody] DeployRequest request)
        {
            if (_decisionService is DecisionService svc)
                await svc.DeployAsync(request.DecisionKey, request.Name, request.DmnXml, request.TenantId);
            return Ok();
        }

        public record DeployRequest(string DecisionKey, string Name, string DmnXml, string? TenantId = null);

        [HttpGet("by-key")]
        public async Task<ActionResult<VertexBPMN.Core.Services.DecisionDefinition>> GetDecisionByKey([FromQuery] string decisionKey, [FromQuery] string? tenantId = null)
        {
            var def = await _decisionService.GetDecisionByKeyAsync(decisionKey, tenantId);
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
        [ProducesResponseType(typeof(VertexBPMN.Core.Services.DecisionResult), 200)]
        public async Task<ActionResult<VertexBPMN.Core.Services.DecisionResult>> Evaluate([FromBody] EvaluateRequest request)
        {
            var result = await _decisionService.EvaluateDecisionByKeyAsync(request.DecisionKey, request.Inputs);
            return Ok(result);
        }

        public record EvaluateRequest(string DecisionKey, IDictionary<string, object> Inputs);
    }
}
