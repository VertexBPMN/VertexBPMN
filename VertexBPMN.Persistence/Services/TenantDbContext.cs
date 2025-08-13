using Microsoft.EntityFrameworkCore;
using VertexBPMN.Core.Domain;

namespace VertexBPMN.Persistence.Services
{
    public class TenantDbContext : DbContext
    {
        public DbSet<Tenant> Tenants { get; set; }
        public TenantDbContext(DbContextOptions<TenantDbContext> options) : base(options) { }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Tenant>().HasKey(t => t.Id);
            modelBuilder.Entity<Tenant>().HasIndex(t => t.Name);
        }
    }
}
