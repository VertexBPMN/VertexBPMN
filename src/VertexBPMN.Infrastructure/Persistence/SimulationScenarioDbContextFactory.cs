using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using VertexBPMN.Infrastructure.Persistence.Services;

namespace VertexBPMN.Infrastructure.Persistence;

public class SimulationScenarioDbContextFactory : IDesignTimeDbContextFactory<SimulationScenarioDbContext>
{
    public SimulationScenarioDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<SimulationScenarioDbContext>();
        optionsBuilder.UseSqlite("Data Source=vertexbpmn_simulation.db");
        return new SimulationScenarioDbContext(optionsBuilder.Options);
    }
}
