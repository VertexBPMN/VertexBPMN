using Microsoft.EntityFrameworkCore;
using VertexBPMN.Core.Services;

namespace VertexBPMN.Persistence.Services
{
    public class ProcessMiningEventDbContext : DbContext
    {
        public DbSet<ProcessMiningEvent> Events { get; set; }
        public ProcessMiningEventDbContext(DbContextOptions<ProcessMiningEventDbContext> options) : base(options) { }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ProcessMiningEvent>().HasKey(e => e.Id);
            modelBuilder.Entity<ProcessMiningEvent>().HasIndex(e => e.EventType);
            modelBuilder.Entity<ProcessMiningEvent>().HasIndex(e => e.ProcessInstanceId);
            modelBuilder.Entity<ProcessMiningEvent>().HasIndex(e => e.TenantId);
            modelBuilder.Entity<ProcessMiningEvent>().HasIndex(e => e.Timestamp);
            // Add more mappings as needed
        }
    }
}
