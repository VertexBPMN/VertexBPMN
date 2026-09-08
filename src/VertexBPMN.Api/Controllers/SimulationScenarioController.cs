using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.Api.Controllers
{
    [ApiController]
    [Route("api/simulation-scenario")]
    [ApiExplorerSettings(GroupName = "Simulation")]
    [Authorize]
    public class SimulationScenarioController : ControllerBase
    {
        private readonly ISimulationScenarioService _service;
        public SimulationScenarioController(ISimulationScenarioService service)
        {
            _service = service;
        }

        /// <summary>
        /// Returns all simulation scenarios for a tenant.
        /// Non-admins are always pinned to their own tenant claim.
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Dto.SimulationScenarioDto>>> GetAll([FromQuery] string? tenantId = null)
        {
            var tenant = ResolveTenantId(tenantId) ?? string.Empty;
            var scenarios = await _service.GetAllAsync(tenant);
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
        public async Task<ActionResult<Dto.SimulationScenarioDto>> GetById(string id)
        {
            var scenario = await _service.GetByIdAsync(id);
            if (scenario == null || !CanAccessTenant(scenario.TenantId)) return NotFound();
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
        public async Task<ActionResult<Dto.SimulationScenarioDto>> Create([FromBody] Dto.SimulationScenarioDto dto)
        {
            var scenario = new SimulationScenario
            {
                Name = dto.Name,
                Description = dto.Description,
                ProcessDefinitionId = dto.ProcessDefinitionId,
                Variables = dto.Variables,
                MaxSteps = dto.MaxSteps,
                TenantId = ResolveTenantId(dto.TenantId)
            };
            var created = await _service.CreateAsync(scenario);
            dto.Id = created.Id;
            dto.TenantId = scenario.TenantId;
            return CreatedAtAction(nameof(GetById), new { id = dto.Id }, dto);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<Dto.SimulationScenarioDto>> Update(string id, [FromBody] Dto.SimulationScenarioDto dto)
        {
            var existing = await _service.GetByIdAsync(id);
            if (existing == null || !CanAccessTenant(existing.TenantId)) return NotFound();

            var scenario = new SimulationScenario
            {
                Name = dto.Name,
                Description = dto.Description,
                ProcessDefinitionId = dto.ProcessDefinitionId,
                Variables = dto.Variables,
                MaxSteps = dto.MaxSteps,
                TenantId = ResolveTenantId(dto.TenantId)
            };
            var updated = await _service.UpdateAsync(id, scenario);
            if (updated == null) return NotFound();
            dto.Id = updated.Id;
            return Ok(dto);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(string id)
        {
            var existing = await _service.GetByIdAsync(id);
            if (existing == null || !CanAccessTenant(existing.TenantId)) return NotFound();
            var deleted = await _service.DeleteAsync(id);
            return deleted ? NoContent() : NotFound();
        }

        private string? ResolveTenantId(string? explicitTenantId)
        {
            if (!User.IsInRole("Admin"))
                return User.FindFirstValue("tenant_id");
            return string.IsNullOrWhiteSpace(explicitTenantId) ? null : explicitTenantId;
        }

        private bool CanAccessTenant(string? tenantId)
        {
            if (User.IsInRole("Admin")) return true;
            return string.Equals(tenantId, User.FindFirstValue("tenant_id"), StringComparison.Ordinal);
        }
    }
}
