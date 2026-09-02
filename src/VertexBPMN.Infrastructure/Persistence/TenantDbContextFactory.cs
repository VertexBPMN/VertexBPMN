using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using VertexBPMN.Infrastructure.Persistence.Services;

namespace VertexBPMN.Infrastructure.Persistence;

public class TenantDbContextFactory : IDesignTimeDbContextFactory<TenantDbContext>
{
    public TenantDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<TenantDbContext>();
        optionsBuilder.UseSqlite("Data Source=vertexbpmn_tenants.db");
        return new TenantDbContext(optionsBuilder.Options);
    }
}
