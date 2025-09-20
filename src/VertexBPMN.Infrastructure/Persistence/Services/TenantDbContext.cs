using Microsoft.EntityFrameworkCore;
using VertexBPMN.Domain.Entities;

namespace VertexBPMN.Infrastructure.Persistence.Services
{
    public class TenantDbContext : DbContext
    {
        public DbSet<Tenant> Tenants { get; set; }
        public TenantDbContext(DbContextOptions<TenantDbContext> options) : base(options) { }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Tenant>().HasKey(t => t.Id);
            modelBuilder.Entity<Tenant>().HasIndex(t => t.Name);

            // Seed example tenants
            modelBuilder.Entity<Tenant>().HasData(
                new Tenant { Id = "tenant-default", Name = "Default Tenant", Description = "Standard Mandant", CreatedAt = new DateTime(2025,1,1,0,0,0,DateTimeKind.Utc) },
                new Tenant { Id = "tenant-acme", Name = "Acme Corp", Description = "Beispielkunde", CreatedAt = new DateTime(2025,1,2,0,0,0,DateTimeKind.Utc) }
            );
        }
    }
}
