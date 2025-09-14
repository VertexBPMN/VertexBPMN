using Microsoft.EntityFrameworkCore;
using VertexBPMN.Domain.Entities;

namespace VertexBPMN.Infrastructure.Persistence.Services
{
    public class SimulationScenarioDbContext : DbContext
    {
        public SimulationScenarioDbContext(DbContextOptions<SimulationScenarioDbContext> options) : base(options) { }
        public DbSet<SimulationScenario> Scenarios { get; set; }
    }
}
