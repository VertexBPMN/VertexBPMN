using Microsoft.EntityFrameworkCore;
using VertexBPMN.Core.Domain;

namespace VertexBPMN.Persistence.Services
{
    public class SimulationScenarioDbContext : DbContext
    {
        public SimulationScenarioDbContext(DbContextOptions<SimulationScenarioDbContext> options) : base(options) { }
        public DbSet<SimulationScenario> Scenarios { get; set; }
    }
}
