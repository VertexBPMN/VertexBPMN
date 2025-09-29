using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using VertexBPMN.Domain.Model.Dmn;
using DecisionDefinition = VertexBPMN.Domain.Entities.DecisionDefinition;
using DecisionInstance = VertexBPMN.Domain.Entities.DecisionInstance;

namespace VertexBPMN.Infrastructure.Persistence;

/// <summary>
/// DbContext for DMN decision definitions, evaluations and decision tables.
/// Simplified: DmnDecisionTable (with its Inputs/Outputs/Rules) is stored as JSON columns.
/// </summary>
public class DecisionDbContext : DbContext
{
    public DecisionDbContext(DbContextOptions<DecisionDbContext> options) : base(options) { }

    public DbSet<DecisionDefinition> DecisionDefinitions => Set<DecisionDefinition>();
    public DbSet<DecisionInstance> DecisionInstances => Set<DecisionInstance>();
    public DbSet<DmnDecisionTable> DmnDecisionTables => Set<DmnDecisionTable>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ConfigureDecisionDefinition(modelBuilder);
        ConfigureDecisionInstance(modelBuilder);
        ConfigureDmnDecisionTable(modelBuilder);
        base.OnModelCreating(modelBuilder);
    }

    private static void ConfigureDecisionDefinition(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<DecisionDefinition>();
        entity.ToTable("DecisionDefinitions");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Id).HasMaxLength(200);
        entity.Property(e => e.Key).IsRequired().HasMaxLength(200);
        entity.Property(e => e.Name).IsRequired().HasMaxLength(500);
        entity.Property(e => e.DmnXml).IsRequired();
        entity.Property(e => e.TenantId).HasMaxLength(64);
        // Persist only raw XML; runtime DecisionTable can be resolved separately (optional)
        entity.Ignore(e => e.DecisionTable);
        entity.HasIndex(e => e.Key);
        entity.HasIndex(e => new { e.Key, e.TenantId }).IsUnique();
    }

    private static void ConfigureDecisionInstance(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<DecisionInstance>();
        entity.ToTable("DecisionInstances");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Id).HasMaxLength(100);
        entity.Property(e => e.DecisionDefinitionKey).IsRequired().HasMaxLength(200);
        entity.Property(e => e.TenantId).HasMaxLength(64);
        entity.Property(e => e.ErrorMessage).HasMaxLength(2000);

        entity.Property(e => e.InputVariables)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<Dictionary<string, object>>(v, (JsonSerializerOptions?)null) ?? new())
            .HasColumnType("TEXT");

        entity.Property(e => e.OutputVariables)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<Dictionary<string, object>>(v, (JsonSerializerOptions?)null) ?? new())
            .HasColumnType("TEXT");

        entity.HasIndex(e => e.DecisionDefinitionKey);
        entity.HasIndex(e => e.TenantId);
        entity.HasIndex(e => e.EvaluationTime);
        entity.HasIndex(e => new { e.DecisionDefinitionKey, e.TenantId });
    }

    private static void ConfigureDmnDecisionTable(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<DmnDecisionTable>();
        entity.ToTable("DmnDecisionTables");
        // Use Key as primary key
        entity.HasKey(t => t.Key);
        entity.Property(t => t.Key).HasMaxLength(200);
        entity.Property(t => t.Name).HasMaxLength(500).IsRequired(false);
        entity.Property(t => t.HitPolicy).HasMaxLength(50);

        // JSON serialize the collections (Inputs, Outputs, Rules)
        entity.Property(t => t.Inputs)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<DmnInput>>(v, (JsonSerializerOptions?)null) ?? new())
            .HasColumnType("TEXT");

        entity.Property(t => t.Outputs)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<DmnOutput>>(v, (JsonSerializerOptions?)null) ?? new())
            .HasColumnType("TEXT");

        entity.Property(t => t.Rules)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<DmnRule>>(v, (JsonSerializerOptions?)null) ?? new())
            .HasColumnType("TEXT");

        entity.HasIndex(t => t.Name);
    }
}
