using Microsoft.EntityFrameworkCore;
using VertexBPMN.Domain.Entities;

namespace VertexBPMN.Infrastructure.Persistence.Services
{
    public class SimulationScenarioDbContext : DbContext
    {
        public SimulationScenarioDbContext(DbContextOptions<SimulationScenarioDbContext> options) : base(options) { }
        public DbSet<SimulationScenario> Scenarios { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<SimulationScenario>().HasKey(s => s.Id);
            // Seed sample scenarios (Variables not mapped)
            modelBuilder.Entity<SimulationScenario>().HasData(
                new
                {
                    Id = "sim-sample-1",
                    Name = "Throughput Test",
                    Description = "Ein einfacher Simulationstest",
                    ProcessDefinitionId = "22222222-2222-2222-2222-222222222222",
                    BpmnXml = (string?)null,
                    MaxSteps = 100,
                    TenantId = "tenant-default"
                }
            );
        }
    }
}
