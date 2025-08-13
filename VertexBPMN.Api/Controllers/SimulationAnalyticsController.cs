using VertexBPMN.Core.Services;
using Microsoft.AspNetCore.Mvc;
using VertexBPMN.Core.Domain;
using System.Collections.Generic;
using System.Linq;

namespace VertexBPMN.Api.Controllers
{
    [ApiController]
    [Route("api/simulation-analytics")]
    [ApiExplorerSettings(GroupName = "Simulation")]
    public class SimulationAnalyticsController : ControllerBase
    {
        private readonly ISemanticValidationService _validationService;
        public SimulationAnalyticsController(ISemanticValidationService validationService)
        {
            _validationService = validationService;
        }
        /// <summary>
        /// Compares two simulation results and returns differences in steps, variables, and outcomes.
        /// </summary>
        [HttpPost("compare")]
        public ActionResult<SimulationComparisonDto> CompareScenarios([FromBody] SimulationComparisonRequestDto request)
        {
            var resultA = request.ResultA;
            var resultB = request.ResultB;
            var stepDiff = (resultA?.Steps?.Count ?? 0) - (resultB?.Steps?.Count ?? 0);
            var variableDiff = new Dictionary<string, (object? A, object? B)>();
            var allVars = (resultA?.Steps?.LastOrDefault()?.Variables?.Keys ?? Enumerable.Empty<string>())
                .Union(resultB?.Steps?.LastOrDefault()?.Variables?.Keys ?? Enumerable.Empty<string>()).Distinct();
            foreach (var v in allVars)
            {
                variableDiff[v] = (
                    resultA?.Steps?.LastOrDefault()?.Variables?.ContainsKey(v) == true ? resultA?.Steps?.LastOrDefault()?.Variables?[v] : null,
                    resultB?.Steps?.LastOrDefault()?.Variables?.ContainsKey(v) == true ? resultB?.Steps?.LastOrDefault()?.Variables?[v] : null
                );
            }
            var completedDiff = (resultA?.Completed ?? false) != (resultB?.Completed ?? false);
            var dto = new SimulationComparisonDto
            {
                StepCountDifference = stepDiff,
                VariableDifferences = variableDiff,
                CompletedDifference = completedDiff
            };
            var diagnosticsA = _validationService.ValidateBpmn(resultA?.BpmnXml ?? "");
            var diagnosticsB = _validationService.ValidateBpmn(resultB?.BpmnXml ?? "");
            return Ok(new { Comparison = dto, DiagnosticsA = diagnosticsA, DiagnosticsB = diagnosticsB });
        }
        /// <summary>
        /// Returns the trace of a specific variable across all simulation steps.
        /// </summary>
        [HttpPost("variable-trace/{variableName}")]
        public ActionResult<List<VariableTraceDto>> GetVariableTrace([FromBody] SimulationResult result, string variableName)
        {
            var trace = result.Steps?.Select(s => new VariableTraceDto
            {
                StepNumber = s.StepNumber,
                ActivityId = s.ActivityId,
                Value = s.Variables != null && s.Variables.ContainsKey(variableName) ? s.Variables[variableName] : null
            }).ToList() ?? new List<VariableTraceDto>();
            var diagnostics = _validationService.ValidateBpmn(result.BpmnXml ?? "");
            return Ok(new { Trace = trace, Diagnostics = diagnostics });
        }
        /// <summary>
        /// Returns a breakdown of each step in a simulation result.
        /// </summary>
        [HttpPost("steps")]
        public ActionResult<List<SimulationStepAnalyticsDto>> GetStepBreakdown([FromBody] SimulationResult result)
        {
            var steps = result.Steps?.Select(s => new SimulationStepAnalyticsDto
            {
                StepNumber = s.StepNumber,
                ActivityId = s.ActivityId,
                ActivityName = s.ActivityName,
                Variables = s.Variables,
                Timestamp = s.Timestamp
            }).ToList() ?? new List<SimulationStepAnalyticsDto>();
            var diagnostics = _validationService.ValidateBpmn(result.BpmnXml ?? "");
            return Ok(new { Steps = steps, Diagnostics = diagnostics });
        }

        /// <summary>
        /// Returns summary statistics for a simulation result.
        /// </summary>
        [HttpPost("summary")]
        public ActionResult<SimulationAnalyticsSummaryDto> GetSummary([FromBody] SimulationResult result)
        {
            var summary = new SimulationAnalyticsSummaryDto
            {
                ProcessDefinitionId = result.ProcessDefinitionId,
                TenantId = result.TenantId,
                StepCount = result.Steps?.Count ?? 0,
                Completed = result.Completed,
                Variables = result.Steps?.LastOrDefault()?.Variables ?? new Dictionary<string, object>()
            };
            var diagnostics = _validationService.ValidateBpmn(result.BpmnXml ?? "");
            return Ok(new { Summary = summary, Diagnostics = diagnostics });
        }
    }

    public class SimulationStepAnalyticsDto
    {
        public int StepNumber { get; set; }
        public string? ActivityId { get; set; }
        public string? ActivityName { get; set; }
        public Dictionary<string, object>? Variables { get; set; }
        public System.DateTime Timestamp { get; set; }
    }

    public class SimulationAnalyticsSummaryDto
    {
        public string? ProcessDefinitionId { get; set; }
        public string? TenantId { get; set; }
        public int StepCount { get; set; }
        public bool Completed { get; set; }
        public Dictionary<string, object>? Variables { get; set; }
    }
}
