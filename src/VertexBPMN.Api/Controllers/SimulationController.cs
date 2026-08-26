using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using VertexBPMN.Api.Features;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.Api.Controllers
{
    /// <summary>
    /// Provides BPMN process simulation endpoints.
    /// </summary>
    [ApiController]
    [Route("api/simulation")]
    [ApiExplorerSettings(GroupName = "Simulation")]
    public class SimulationController : ControllerBase
    {
        private readonly ISimulationService _simulationService;
        private readonly ISimulationScenarioService _scenarioService;
        private readonly ISemanticValidationService _validationService;
        private readonly AdvancedFeatureOptions _features;
        public SimulationController(ISimulationService simulationService, ISimulationScenarioService scenarioService, ISemanticValidationService validationService, IOptions<AdvancedFeatureOptions> features)
        {
            _simulationService = simulationService;
            _scenarioService = scenarioService;
            _validationService = validationService;
            _features = features.Value;
        }
        /// <summary>
        /// Simulates a BPMN process instance using a saved scenario.
        /// </summary>
        /// <param name="scenarioId">Scenario ID</param>
        /// <returns>Simulation result with steps and status</returns>
        [HttpPost("scenario/{scenarioId}")]
        [ProducesResponseType(typeof(SimulationResult), 200)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status501NotImplemented)]
        public async System.Threading.Tasks.Task<ActionResult<SimulationResult>> SimulateScenario(string scenarioId)
        {
            if (!_features.SimulationExecution) return SimulationUnavailable();
            var scenario = await _scenarioService.GetByIdAsync(scenarioId);
            if (scenario == null) return NotFound();
            // Validate BPMN before simulation (assume scenario contains BPMN XML)
            var diagnostics = _validationService.ValidateBpmn(scenario.BpmnXml ?? "");
            var request = new SimulationRequest
            {
                ProcessDefinitionId = scenario.ProcessDefinitionId,
                Variables = scenario.Variables,
                MaxSteps = scenario.MaxSteps,
                TenantId = scenario.TenantId
            };
            var result = await _simulationService.SimulateAsync(request);
            return Ok(new { Simulation = result, Diagnostics = diagnostics });
        }
        /// <summary>
        /// Simulates a BPMN process instance and returns the simulation steps.
        /// </summary>
        /// <param name="request">Simulation request DTO</param>
        /// <returns>Simulation result with steps and status</returns>
        [HttpPost]
        [ProducesResponseType(typeof(SimulationResult), 200)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status501NotImplemented)]
        public async System.Threading.Tasks.Task<ActionResult<SimulationResult>> Simulate([FromBody] Dto.SimulationRequestDto request)
        {
            if (!_features.SimulationExecution) return SimulationUnavailable();
            var domainRequest = new SimulationRequest
            {
                ProcessDefinitionId = request.ProcessDefinitionId,
                Variables = request.Variables,
                MaxSteps = request.MaxSteps,
                TenantId = request.TenantId
            };
            // Validate BPMN before simulation (assume request contains BPMN XML)
            var diagnostics = _validationService.ValidateBpmn(request.BpmnXml ?? "");
            var result = await _simulationService.SimulateAsync(domainRequest);
            return Ok(new { Simulation = result, Diagnostics = diagnostics });
        }

        private ObjectResult SimulationUnavailable() => Problem(
            statusCode: StatusCodes.Status501NotImplemented,
            title: "BPMN simulation is not qualified for production use",
            detail: "Simulation execution is disabled until a real engine-backed, deterministic simulation passes the Phase 4 acceptance gate.");
    }
}
