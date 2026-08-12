using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace VertexBPMN.Infrastructure.Persistence;

public class DecisionDbContextFactory : IDesignTimeDbContextFactory<DecisionDbContext>
{
    public DecisionDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<DecisionDbContext>();
        optionsBuilder.UseSqlite("Data Source=vertexbpmn_decisions.db");
        return new DecisionDbContext(optionsBuilder.Options);
    }
}
