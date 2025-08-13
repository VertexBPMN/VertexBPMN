using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Sqlite;
using VertexBPMN.Core.Services;
using VertexBPMN.Persistence.Services;

namespace VertexBPMN.Persistence.Services
{
    public class ProcessMiningEventDbContextFactory : IDesignTimeDbContextFactory<ProcessMiningEventDbContext>
    {
        public static ProcessMiningEventDbContext Create(string connectionString)
        {
            var options = new DbContextOptionsBuilder<ProcessMiningEventDbContext>()
                .UseSqlite(connectionString)
                .Options;
            return new ProcessMiningEventDbContext(options);
        }

        // For EF Core design-time migration support
        public ProcessMiningEventDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<ProcessMiningEventDbContext>();
            optionsBuilder.UseSqlite("Data Source=vertexbpmn_events.db");
            return new ProcessMiningEventDbContext(optionsBuilder.Options);
        }
    }
}
