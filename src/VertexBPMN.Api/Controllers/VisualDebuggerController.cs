using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Entities.Debugging;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Domain.Interfaces.Repositories;

namespace VertexBPMN.Api.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/visual-debugger")]
    [ApiExplorerSettings(GroupName = "Debugger")]
    public class VisualDebuggerController : ControllerBase
    {
        private readonly IRuntimeService _runtimeService;
        private readonly IVisualDebugStepService _stepService;

        public VisualDebuggerController(
            IRuntimeService runtimeService,
            IVisualDebugStepService stepService)
        {
            _runtimeService = runtimeService;
            _stepService = stepService;
        }

        /// <summary>
        /// Get the current state of a process instance for visual debugging.
        /// </summary>
        [HttpGet("instance/{id}/state")]
        [ProducesResponseType(typeof(ProcessInstance), 200)]
        public async Task<ActionResult<ProcessInstance>> GetInstanceState(Guid id, CancellationToken cancellationToken)
        {
            var instance = await _runtimeService.GetByIdAsync(id, cancellationToken);
            if (instance == null) return NotFound();

            // Fetch real BPMN XML for the process definition
            string bpmnXml = string.Empty;
            if (instance.ProcessDefinitionId != Guid.Empty)
            {
                var repoService = HttpContext.RequestServices.GetService(typeof(IRepositoryService)) as IRepositoryService;
                if (repoService != null)
                {
                    var def = await repoService.GetByIdAsync(instance.ProcessDefinitionId, cancellationToken);
                    bpmnXml = def?.BpmnXml ?? string.Empty;
                }
            }

            // Fetch real tokens using IExecutionTokenRepository
            var tokenRepo = HttpContext.RequestServices.GetService(typeof(IExecutionTokenRepository)) as IExecutionTokenRepository;
            var tokens = new List<ExecutionToken>();
            if (tokenRepo != null)
            {
                await foreach (var token in tokenRepo.ListByProcessInstanceAsync(id, cancellationToken))
                    tokens.Add(token);
            }

            // Fetch real variables using IVariableRepository
            var variableRepo = HttpContext.RequestServices.GetService(typeof(IVariableRepository)) as IVariableRepository;
            var variables = new List<Variable>();
            if (variableRepo != null)
            {
                await foreach (var variable in variableRepo.ListByScopeAsync(id, cancellationToken))
                    variables.Add(variable);
            }

            // Fetch real multi-instances using IMultiInstanceExecutionRepository
            var multiInstanceRepo = HttpContext.RequestServices.GetService(typeof(IMultiInstanceExecutionRepository)) as IMultiInstanceExecutionRepository;
            var multiInstances = new List<MultiInstanceExecution>();
            if (multiInstanceRepo != null)
            {
                await foreach (var mi in multiInstanceRepo.ListByProcessInstanceAsync(id, cancellationToken))
                    multiInstances.Add(mi);
            }

            // Use current activity from first token if available
            string currentActivityId = tokens.Count > 0 ? tokens[0].CurrentNodeId : string.Empty;

            var state = new {
                Instance = instance,
                BpmnXml = bpmnXml,
                CurrentActivityId = currentActivityId,
                Tokens = tokens,
                Variables = variables,
                MultiInstances = multiInstances
            };
            return Ok(state);
        }

        /// <summary>
        /// Step the process instance to the next activity (Step-API).
        /// </summary>
        [HttpPost("instance/{id}/step")]
        [ProducesResponseType(typeof(VisualDebugStepResult), 200)]
        public async Task<ActionResult<VisualDebugStepResult>> StepInstance(Guid id, CancellationToken cancellationToken)
        {
            var instance = await _runtimeService.GetByIdAsync(id, cancellationToken);
            if (instance == null) return NotFound();
            if (!CanAccessTenant(instance.TenantId)) return Forbid();

            try
            {
                return Ok(await _stepService.StepAsync(id, cancellationToken));
            }
            catch (InvalidOperationException exception)
            {
                return Conflict(new ProblemDetails
                {
                    Title = "Visual stepping cannot advance this process instance",
                    Detail = exception.Message,
                    Status = StatusCodes.Status409Conflict
                });
            }
        }

        private bool CanAccessTenant(string? tenantId) =>
            User.IsInRole("Admin") ||
            string.Equals(User.FindFirstValue("tenant_id"), tenantId, StringComparison.Ordinal);

    }
}
