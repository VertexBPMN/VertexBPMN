using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using VertexBPMN.Core.Domain;
using VertexBPMN.Core.Services;
using System.Collections.Generic;

namespace VertexBPMN.Api.Controllers
{
    [ApiController]
    [Route("api/simulation-scenario")]
    [ApiExplorerSettings(GroupName = "Simulation")]
    public class SimulationScenarioController : ControllerBase
    {
        private readonly ISimulationScenarioService _service;
        public SimulationScenarioController(ISimulationScenarioService service)
        {
            _service = service;
        }

        /// <summary>
        /// Returns all simulation scenarios for a tenant.
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Dto.SimulationScenarioDto>>> GetAll([FromQuery] string? tenantId = null)
        {
            var scenarios = await _service.GetAllAsync(tenantId ?? string.Empty);
            var dtos = scenarios.Select(s => new Dto.SimulationScenarioDto
            {
                Id = s.Id,
                Name = s.Name,
                Description = s.Description,
                ProcessDefinitionId = s.ProcessDefinitionId,
                Variables = s.Variables,
                MaxSteps = s.MaxSteps,
                TenantId = s.TenantId
            });
            return Ok(dtos);
        }

        [HttpGet("{id}")]
        public async System.Threading.Tasks.Task<ActionResult<Dto.SimulationScenarioDto>> GetById(string id)
        {
            var scenario = await _service.GetByIdAsync(id);
            if (scenario == null) return NotFound();
            var dto = new Dto.SimulationScenarioDto
            {
                Id = scenario.Id,
                Name = scenario.Name,
                Description = scenario.Description,
                ProcessDefinitionId = scenario.ProcessDefinitionId,
                Variables = scenario.Variables,
                MaxSteps = scenario.MaxSteps,
                TenantId = scenario.TenantId
            };
            return Ok(dto);
        }

        [HttpPost]
        public async System.Threading.Tasks.Task<ActionResult<Dto.SimulationScenarioDto>> Create([FromBody] Dto.SimulationScenarioDto dto)
        {
            var scenario = new SimulationScenario
            {
                Name = dto.Name,
                Description = dto.Description,
                ProcessDefinitionId = dto.ProcessDefinitionId,
                Variables = dto.Variables,
                MaxSteps = dto.MaxSteps,
                TenantId = dto.TenantId
            };
            var created = await _service.CreateAsync(scenario);
            dto.Id = created.Id;
            return CreatedAtAction(nameof(GetById), new { id = dto.Id }, dto);
        }

        [HttpPut("{id}")]
        public async System.Threading.Tasks.Task<ActionResult<Dto.SimulationScenarioDto>> Update(string id, [FromBody] Dto.SimulationScenarioDto dto)
        {
            var scenario = new SimulationScenario
            {
                Name = dto.Name,
                Description = dto.Description,
                ProcessDefinitionId = dto.ProcessDefinitionId,
                Variables = dto.Variables,
                MaxSteps = dto.MaxSteps,
                TenantId = dto.TenantId
            };
            var updated = await _service.UpdateAsync(id, scenario);
            if (updated == null) return NotFound();
            dto.Id = updated.Id;
            return Ok(dto);
        }

        [HttpDelete("{id}")]
        public async System.Threading.Tasks.Task<IActionResult> Delete(string id)
        {
            var deleted = await _service.DeleteAsync(id);
            if (!deleted) return NotFound();
            return NoContent();
        }
    }
}
