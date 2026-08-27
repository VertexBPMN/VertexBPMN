using Microsoft.AspNetCore.Mvc;
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
        public SimulationController(
            ISimulationService simulationService,
            ISimulationScenarioService scenarioService,
            ISemanticValidationService validationService)
        {
            _simulationService = simulationService;
            _scenarioService = scenarioService;
            _validationService = validationService;
        }
        /// <summary>
        /// Simulates a BPMN process instance using a saved scenario.
        /// </summary>
        /// <param name="scenarioId">Scenario ID</param>
        /// <returns>Simulation result with steps and status</returns>
        [HttpPost("scenario/{scenarioId}")]
        [ProducesResponseType(typeof(SimulationResult), 200)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        public async System.Threading.Tasks.Task<ActionResult<SimulationResult>> SimulateScenario(string scenarioId)
        {
            var scenario = await _scenarioService.GetByIdAsync(scenarioId);
            if (scenario == null) return NotFound();
            // Validate BPMN before simulation (assume scenario contains BPMN XML)
            var diagnostics = _validationService.ValidateBpmn(scenario.BpmnXml ?? "");
            var request = new SimulationRequest
            {
                BpmnXml = scenario.BpmnXml ?? string.Empty,
                ProcessDefinitionId = scenario.ProcessDefinitionId,
                Variables = scenario.Variables,
                MaxSteps = scenario.MaxSteps,
                TenantId = scenario.TenantId
            };
            return await SimulateValidated(request, diagnostics);
        }
        /// <summary>
        /// Simulates a BPMN process instance and returns the simulation steps.
        /// </summary>
        /// <param name="request">Simulation request DTO</param>
        /// <returns>Simulation result with steps and status</returns>
        [HttpPost]
        [ProducesResponseType(typeof(SimulationResult), 200)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        public async System.Threading.Tasks.Task<ActionResult<SimulationResult>> Simulate([FromBody] Dto.SimulationRequestDto request)
        {
            var domainRequest = new SimulationRequest
            {
                BpmnXml = request.BpmnXml ?? string.Empty,
                ProcessDefinitionId = request.ProcessDefinitionId,
                Variables = request.Variables,
                MaxSteps = request.MaxSteps,
                TenantId = request.TenantId,
                EventSelections = request.EventSelections
            };
            // Validate BPMN before simulation (assume request contains BPMN XML)
            var diagnostics = _validationService.ValidateBpmn(request.BpmnXml ?? "");
            return await SimulateValidated(domainRequest, diagnostics);
        }

        private async Task<ActionResult<SimulationResult>> SimulateValidated(
            SimulationRequest request,
            SemanticValidationResult diagnostics)
        {
            try
            {
                var result = await _simulationService.SimulateAsync(request, HttpContext.RequestAborted);
                return Ok(new { Simulation = result, Diagnostics = diagnostics });
            }
            catch (Exception exception) when (exception is ArgumentException
                                              or InvalidOperationException
                                              or NotSupportedException)
            {
                return Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "BPMN simulation request is not executable",
                    detail: exception.Message);
            }
        }
    }
}
