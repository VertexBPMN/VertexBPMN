using Microsoft.EntityFrameworkCore;

namespace VertexBPMN.Infrastructure.Persistence;

public sealed class DependencyRegistryDbContext(DbContextOptions<DependencyRegistryDbContext> options) : DbContext(options)
{
    public DbSet<DependencyConfigurationEntity> Entries => Set<DependencyConfigurationEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DependencyConfigurationEntity>(entity =>
        {
            entity.HasKey(entry => entry.Key);
            entity.Property(entry => entry.Key).HasMaxLength(256);
            entity.Property(entry => entry.Value).IsRequired();
        });
    }
}

public sealed class DependencyConfigurationEntity
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; }
}
