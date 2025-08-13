using Microsoft.EntityFrameworkCore;
using VertexBPMN.Core.Domain;

namespace VertexBPMN.Persistence;

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
    public DbSet<VertexBPMN.Core.Domain.Task> Tasks => Set<VertexBPMN.Core.Domain.Task>();
    public DbSet<HistoryEvent> HistoryEvents => Set<HistoryEvent>();
    public DbSet<Incident> Incidents => Set<Incident>();
    public DbSet<MultiInstanceExecution> MultiInstanceExecutions => Set<MultiInstanceExecution>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
    // TODO: Configure entity relationships, keys, indexes, and constraints
    modelBuilder.Entity<MultiInstanceExecution>().HasKey(e => e.Id);
    base.OnModelCreating(modelBuilder);
    }
}
