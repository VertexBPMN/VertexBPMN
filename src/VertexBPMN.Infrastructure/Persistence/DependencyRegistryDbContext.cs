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

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0048", Justification = "Small entity type kept adjacent to the DbContext that uses it; splitting it into a separate file adds no value.")]
public sealed class DependencyConfigurationEntity
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; }
}
