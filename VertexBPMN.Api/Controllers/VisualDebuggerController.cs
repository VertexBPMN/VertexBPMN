using Microsoft.AspNetCore.Mvc;
using VertexBPMN.Core.Services;
using VertexBPMN.Core.Domain;
using System;
using System.Threading;
using VertexBPMN.Persistence.Repositories;
using System.Threading.Tasks;

namespace VertexBPMN.Api.Controllers
{
    [ApiController]
    [Route("api/visual-debugger")]
    [ApiExplorerSettings(GroupName = "Debugger")]
    public class VisualDebuggerController : ControllerBase
    {
        private readonly IRuntimeService _runtimeService;
        public VisualDebuggerController(IRuntimeService runtimeService)
        {
            _runtimeService = runtimeService;
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
            string currentActivityId = tokens.Count > 0 ? tokens[0].NodeId : string.Empty;

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
        [ProducesResponseType(typeof(ProcessInstance), 200)]
        public async Task<ActionResult<ProcessInstance>> StepInstance(Guid id, CancellationToken cancellationToken)
        {
            var instance = await _runtimeService.GetByIdAsync(id, cancellationToken);
            if (instance == null) return NotFound();

            // Simulate step-through: advance token to next activity (demo logic)
            // In production, this should invoke engine logic to move the token
            if (!string.IsNullOrEmpty(instance.State))
            {
                // Example: append step marker
                instance.State += " -> Stepped";
            }
            else
            {
                instance.State = "Stepped";
            }
            // TODO: Persist updated state if needed
            return Ok(instance);
        }
    }
}
