using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace VertexBPMN.Infrastructure.Persistence;

public class BpmnDbContextFactory : IDesignTimeDbContextFactory<BpmnDbContext>
{
    public BpmnDbContext CreateDbContext(string[] args)
    {
    var optionsBuilder = new DbContextOptionsBuilder<BpmnDbContext>();
    optionsBuilder.UseSqlite("Data Source=vertexbpmn.db");
    return new BpmnDbContext(optionsBuilder.Options);
    }
}
