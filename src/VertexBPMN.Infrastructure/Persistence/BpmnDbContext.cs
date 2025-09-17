using Microsoft.EntityFrameworkCore;
using VertexBPMN.Domain.Entities;
using System.Text.Json;

namespace VertexBPMN.Infrastructure.Persistence;

/// <summary>
/// EF Core DbContext for BPMN engine persistence.
/// </summary>
public class BpmnDbContext : DbContext
{
    public BpmnDbContext(DbContextOptions<BpmnDbContext> options) : base(options) { }

    public DbSet<ProcessDefinition> ProcessDefinitions => Set<ProcessDefinition>();
    public DbSet<ProcessInstance> ProcessInstances => Set<ProcessInstance>();
    public DbSet<ExecutionToken> ExecutionTokens => Set<ExecutionToken>();
    public DbSet<Variable> Variables => Set<Variable>();
    public DbSet<Job> Jobs => Set<Job>();
    public DbSet<UserTask> Tasks => Set<UserTask>();
    public DbSet<HistoryEvent> HistoryEvents => Set<HistoryEvent>();
    public DbSet<Incident> Incidents => Set<Incident>();
    public DbSet<MultiInstanceExecution> MultiInstanceExecutions => Set<MultiInstanceExecution>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ConfigureProcessDefinition(modelBuilder);
        ConfigureProcessInstance(modelBuilder);
        ConfigureExecutionToken(modelBuilder);
        ConfigureVariable(modelBuilder);
        ConfigureJob(modelBuilder);
        ConfigureUserTask(modelBuilder);
        ConfigureHistoryEvent(modelBuilder);
        ConfigureIncident(modelBuilder);
        ConfigureMultiInstanceExecution(modelBuilder);
        
        base.OnModelCreating(modelBuilder);
    }

    private static void ConfigureProcessDefinition(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<ProcessDefinition>();
        
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Key).IsRequired().HasMaxLength(255);
        entity.Property(e => e.Name).IsRequired().HasMaxLength(500);
        entity.Property(e => e.BpmnXml).IsRequired();
        entity.Property(e => e.TenantId).HasMaxLength(64);
        
        // Indexes
        entity.HasIndex(e => e.Key);
        entity.HasIndex(e => new { e.Key, e.Version }).IsUnique();
        entity.HasIndex(e => e.TenantId);
        entity.HasIndex(e => e.DeploymentId);
    }

    private static void ConfigureProcessInstance(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<ProcessInstance>();
        
        entity.HasKey(e => e.Id);
        entity.Property(e => e.BusinessKey).HasMaxLength(255);
        entity.Property(e => e.TenantId).HasMaxLength(64);
        entity.Property(e => e.State).IsRequired().HasMaxLength(50);
        entity.Property(e => e.InstanceId).IsRequired().HasMaxLength(255);
        entity.Property(e => e.ProcessId).IsRequired().HasMaxLength(255);
        
        // Complex type conversions
        entity.Property(e => e.Variables)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null!),
                v => JsonSerializer.Deserialize<Dictionary<string, object>>(v, (JsonSerializerOptions)null!) ?? new Dictionary<string, object>());
        
        entity.Property(e => e.ActiveTasks)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null!),
                v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions)null!) ?? new List<string>());
        
        entity.Property(e => e.ActiveTokens)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null!),
                v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions)null!) ?? new List<string>());

        // Relationships
        entity.HasOne(e => e.ProcessDefinition)
            .WithMany()
            .HasForeignKey(e => e.ProcessDefinitionId)
            .OnDelete(DeleteBehavior.Restrict);
        
        // Indexes
        entity.HasIndex(e => e.ProcessDefinitionId);
        entity.HasIndex(e => e.BusinessKey);
        entity.HasIndex(e => e.TenantId);
        entity.HasIndex(e => e.State);
        entity.HasIndex(e => e.StartedAt);
    }

    private static void ConfigureExecutionToken(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<ExecutionToken>();
        
        entity.HasKey(e => e.Id);
        entity.Property(e => e.CurrentNodeId).IsRequired().HasMaxLength(255);
        entity.Property(e => e.NodeType).IsRequired().HasMaxLength(100);
        entity.Property(e => e.State).IsRequired().HasMaxLength(50);
        entity.Property(e => e.AssignedWorker).HasMaxLength(255);
        
        // Complex type conversion for Variables dictionary
        entity.Property(e => e.Variables)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null!),
                v => JsonSerializer.Deserialize<Dictionary<string, object>>(v, (JsonSerializerOptions)null!) ?? new Dictionary<string, object>());
        
        // Indexes
        entity.HasIndex(e => e.ProcessInstanceId);
        entity.HasIndex(e => e.CurrentNodeId);
        entity.HasIndex(e => e.State);
        entity.HasIndex(e => e.AssignedWorker);
        entity.HasIndex(e => e.CreatedAt);
    }

    private static void ConfigureVariable(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<Variable>();
        
        entity.HasKey(e => e.Id);
        // Additional configuration would be needed based on Variable entity structure
        
        // Indexes would be added based on Variable properties
    }

    private static void ConfigureJob(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<Job>();
        
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Type).IsRequired().HasMaxLength(100);
        entity.Property(e => e.State).IsRequired().HasMaxLength(50);
        entity.Property(e => e.TenantId).HasMaxLength(64);
        entity.Property(e => e.ErrorMessage).HasMaxLength(4000);
        
        // Relationships
        entity.HasOne(e => e.ProcessInstance)
            .WithMany()
            .HasForeignKey(e => e.ProcessInstanceId)
            .OnDelete(DeleteBehavior.Cascade);
        
        // Indexes
        entity.HasIndex(e => e.ProcessInstanceId);
        entity.HasIndex(e => e.Type);
        entity.HasIndex(e => e.State);
        entity.HasIndex(e => e.DueDate);
        entity.HasIndex(e => e.TenantId);
    }

    private static void ConfigureUserTask(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<UserTask>();
        
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Name).IsRequired().HasMaxLength(500);
        entity.Property(e => e.Type).IsRequired().HasMaxLength(100);
        
        // Indexes
        entity.HasIndex(e => e.ProcessInstanceId);
        entity.HasIndex(e => e.Type);
    }

    private static void ConfigureHistoryEvent(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<HistoryEvent>();
        
        entity.HasKey(e => e.Id);
        entity.Property(e => e.EventType).IsRequired().HasMaxLength(100);
        entity.Property(e => e.ElementId).IsRequired().HasMaxLength(255);
        entity.Property(e => e.TenantId).HasMaxLength(64);
        entity.Property(e => e.Details).HasMaxLength(4000);
        entity.Property(e => e.Data).HasMaxLength(4000);
        
        // Relationships
        entity.HasOne(e => e.ProcessInstance)
            .WithMany()
            .HasForeignKey(e => e.ProcessInstanceId)
            .OnDelete(DeleteBehavior.Cascade);
        
        // Indexes
        entity.HasIndex(e => e.ProcessInstanceId);
        entity.HasIndex(e => e.EventType);
        entity.HasIndex(e => e.ElementId);
        entity.HasIndex(e => e.Timestamp);
        entity.HasIndex(e => e.TenantId);
    }

    private static void ConfigureIncident(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<Incident>();
        
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Type).IsRequired().HasMaxLength(100);
        entity.Property(e => e.Message).IsRequired().HasMaxLength(4000);
        entity.Property(e => e.State).IsRequired().HasMaxLength(50);
        entity.Property(e => e.TenantId).HasMaxLength(64);
        
        // Relationships
        entity.HasOne(e => e.ProcessInstance)
            .WithMany()
            .HasForeignKey(e => e.ProcessInstanceId)
            .OnDelete(DeleteBehavior.Cascade);
        
        // Indexes
        entity.HasIndex(e => e.ProcessInstanceId);
        entity.HasIndex(e => e.Type);
        entity.HasIndex(e => e.State);
        entity.HasIndex(e => e.CreatedAt);
        entity.HasIndex(e => e.TenantId);
    }

    private static void ConfigureMultiInstanceExecution(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<MultiInstanceExecution>();
        
        entity.HasKey(e => e.Id);
        entity.Property(e => e.ActivityId).IsRequired().HasMaxLength(255);
        
        // Indexes
        entity.HasIndex(e => e.ProcessInstanceId);
        entity.HasIndex(e => e.ActivityId);
        entity.HasIndex(e => new { e.ProcessInstanceId, e.ActivityId });
    }
}
