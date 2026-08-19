using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace VertexBPMN.Infrastructure.Persistence;

public class BpmnDbContextFactory : IDesignTimeDbContextFactory<BpmnDbContext>
{
    public BpmnDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<BpmnDbContext>();
        var configuredConnection = Environment.GetEnvironmentVariable("ConnectionStrings__BpmnDbContext");
        var connectionString = string.IsNullOrWhiteSpace(configuredConnection)
            ? "Data Source=dev-bpmn.db"
            : configuredConnection;
        optionsBuilder.UseSqlite(connectionString);
        return new BpmnDbContext(optionsBuilder.Options);
    }
}
