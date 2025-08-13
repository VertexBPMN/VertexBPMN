using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using VertexBPMN.Core.Domain;
using VertexBPMN.Core.Services;

namespace VertexBPMN.Persistence.Services
{
    public class SimulationScenarioService : ISimulationScenarioService
    {
        private readonly SimulationScenarioDbContext _db;
        public SimulationScenarioService(SimulationScenarioDbContext db)
        {
            _db = db;
        }

        public async Task<IEnumerable<SimulationScenario>> GetAllAsync(string tenantId = null)
        {
            if (string.IsNullOrEmpty(tenantId))
                return await _db.Scenarios.ToListAsync();
            return await _db.Scenarios.Where(s => s.TenantId == tenantId).ToListAsync();
        }

        public async Task<SimulationScenario> GetByIdAsync(string id)
        {
            return await _db.Scenarios.FindAsync(id);
        }

        public async Task<SimulationScenario> CreateAsync(SimulationScenario scenario)
        {
            scenario.Id = System.Guid.NewGuid().ToString();
            _db.Scenarios.Add(scenario);
            await _db.SaveChangesAsync();
            return scenario;
        }

        public async Task<SimulationScenario> UpdateAsync(string id, SimulationScenario scenario)
        {
            var existing = await _db.Scenarios.FindAsync(id);
            if (existing == null) return null;
            existing.Name = scenario.Name;
            existing.Description = scenario.Description;
            existing.ProcessDefinitionId = scenario.ProcessDefinitionId;
            existing.Variables = scenario.Variables;
            existing.MaxSteps = scenario.MaxSteps;
            existing.TenantId = scenario.TenantId;
            await _db.SaveChangesAsync();
            return existing;
        }

        public async Task<bool> DeleteAsync(string id)
        {
            var scenario = await _db.Scenarios.FindAsync(id);
            if (scenario == null) return false;
            _db.Scenarios.Remove(scenario);
            await _db.SaveChangesAsync();
            return true;
        }
    }
}
