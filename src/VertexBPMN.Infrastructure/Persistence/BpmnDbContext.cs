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
    public DbSet<EngineDeployment> EngineDeployments => Set<EngineDeployment>();
    public DbSet<User> Users => Set<User>();
    public DbSet<MigrationPlanRecord> MigrationPlans => Set<MigrationPlanRecord>();
    public DbSet<MigrationExecutionRecord> MigrationExecutions => Set<MigrationExecutionRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ConfigureEngineDeployment(modelBuilder);
        ConfigureProcessDefinition(modelBuilder);
        ConfigureProcessInstance(modelBuilder);
        ConfigureExecutionToken(modelBuilder);
        ConfigureVariable(modelBuilder);
        ConfigureJob(modelBuilder);
        ConfigureUserTask(modelBuilder);
        ConfigureHistoryEvent(modelBuilder);
        ConfigureIncident(modelBuilder);
        ConfigureMultiInstanceExecution(modelBuilder);
        ConfigureUser(modelBuilder);
        ConfigureMigrationRecords(modelBuilder);

        // Seed sample data (deterministic IDs & timestamps)
        var deploymentId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var processDefinitionId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var processInstanceId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var jobId = Guid.Parse("44444444-4444-4444-4444-444444444444");
        var taskId = Guid.Parse("55555555-5555-5555-5555-555555555555");
        var variableId = Guid.Parse("66666666-6666-6666-6666-666666666666");
        var historyEventId = Guid.Parse("77777777-7777-7777-7777-777777777777");
        var incidentId = Guid.Parse("88888888-8888-8888-8888-888888888888");
        var miExecutionId = Guid.Parse("99999999-9999-9999-9999-999999999999");
        var tokenId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var seedTimestamp = new DateTime(2025, 01, 01, 0, 0, 0, DateTimeKind.Utc);

        modelBuilder.Entity<EngineDeployment>().HasData(new EngineDeployment
        {
            Id = deploymentId,
            Name = "SampleDeployment",
            CreatedAt = seedTimestamp,
            TenantId = null
        });

        modelBuilder.Entity<ProcessDefinition>().HasData(new ProcessDefinition
        {
            Id = processDefinitionId,
            Key = "SampleProcess",
            Name = "Sample Process",
            Version = 1,
            BpmnXml = "<definitions id='SampleProcess'></definitions>",
            CreatedAt = seedTimestamp,
            DeploymentId = deploymentId,
            TenantId = null
        });

        modelBuilder.Entity<ProcessInstance>().HasData(new ProcessInstance
        {
            Id = processInstanceId,
            ProcessDefinitionId = processDefinitionId,
            BusinessKey = "BK-001",
            TenantId = null,
            StartedAt = seedTimestamp,
            EndedAt = null,
            State = "Running",
            InstanceId = "sample-instance-1",
            ProcessId = "SampleProcess",
            Status = ProcessInstanceStatus.Running,
            ActiveTasks = new List<string>(),
            ActiveTokens = new List<string>(),
            Variables = new Dictionary<string, object>(),
            CreatedAt = seedTimestamp,
            LastModified = seedTimestamp
        });

        modelBuilder.Entity<Job>().HasData(new Job
        {
            Id = jobId,
            ProcessInstanceId = processInstanceId,
            Type = "timer",
            DueDate = seedTimestamp.AddHours(1),
            Retries = 3,
            ErrorMessage = null,
            TenantId = null,
            State = "Scheduled",
            Payload = null
        });

        modelBuilder.Entity<UserTask>().HasData(new UserTask
        {
            Id = taskId,
            ProcessInstanceId = processInstanceId,
            Name = "Review Request",
            Type = "userTask",
            Assignee = null,
            TenantId = null,
            CreatedAt = seedTimestamp,
            CompletedAt = null,
            DueDate = seedTimestamp.AddDays(2),
            FormKey = null,
            FormSchema = null,
            LastModified = seedTimestamp,
            ModifiedBy = string.Empty,
            Status = UserTaskStatus.Pending,
            CandidateUsers = new List<string>(),
            CandidateRole = string.Empty,
            RequiredFields = new List<string>()
        });

        modelBuilder.Entity<Variable>().HasData(new
        {
            Id = variableId,
            ScopeId = processInstanceId,
            Name = "approvalRequired",
            Type = "boolean",
            Value = "true",
            TenantId = (string?)null,
            ProcessInstanceId = processInstanceId,
            CreatedAt = seedTimestamp
        });

        modelBuilder.Entity<HistoryEvent>().HasData(new HistoryEvent
        {
            Id = historyEventId,
            ProcessInstanceId = processInstanceId,
            EventType = "PROCESS_STARTED",
            Timestamp = seedTimestamp,
            Details = "Process instance started.",
            TenantId = null,
            ElementId = "startEvent1",
            Data = null
        });

        modelBuilder.Entity<Incident>().HasData(new Incident
        {
            Id = incidentId,
            ProcessInstanceId = processInstanceId,
            Type = "None",
            Message = "No incident",
            CreatedAt = seedTimestamp,
            TenantId = null,
            State = "Resolved"
        });

        modelBuilder.Entity<MultiInstanceExecution>().HasData(new MultiInstanceExecution
        {
            Id = miExecutionId,
            ProcessInstanceId = processInstanceId,
            ActivityId = "activity_multi_1",
            InstanceCount = 3,
            CompletedCount = 0,
            IsSequential = true
        });

        modelBuilder.Entity<ExecutionToken>().HasData(new ExecutionToken
        {
            Id = tokenId,
            ProcessInstanceId = processInstanceId,
            CurrentNodeId = "startEvent1",
            NodeType = "startEvent",
            Variables = new Dictionary<string, object>(),
            CreatedAt = seedTimestamp,
            AssignedWorker = null,
            AssignedAt = null,
            RetryCount = 0,
            State = "Active"
        });

        // Seed initial users (align with IdentityService defaults)
        modelBuilder.Entity<User>().HasData(
            new User { Id = "1", Username = "admin", Email = "admin@example.com", IsActive = true, Roles = new List<string> { "admin" }, CreatedAt = seedTimestamp, LastModified = seedTimestamp },
            new User { Id = "2", Username = "user1", Email = "user1@example.com", IsActive = true, Roles = new List<string> { "user" }, CreatedAt = seedTimestamp, LastModified = seedTimestamp },
            new User { Id = "3", Username = "user2", Email = "user2@example.com", IsActive = true, Roles = new List<string> { "user" }, CreatedAt = seedTimestamp, LastModified = seedTimestamp }
        );

        base.OnModelCreating(modelBuilder);
    }

    private static void ConfigureMigrationRecords(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MigrationPlanRecord>(entity =>
        {
            entity.HasKey(record => record.Id);
            entity.Property(record => record.Payload).IsRequired();
            entity.HasIndex(record => record.CreatedAt);
        });

        modelBuilder.Entity<MigrationExecutionRecord>(entity =>
        {
            entity.HasKey(record => record.Id);
            entity.Property(record => record.Payload).IsRequired();
            entity.HasIndex(record => record.MigrationPlanId);
            entity.HasIndex(record => record.StartedAt);
        });
    }

    private static void ConfigureEngineDeployment(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<EngineDeployment>();

        entity.HasKey(e => e.Id);
        entity.Property(e => e.Name).IsRequired().HasMaxLength(500);
        entity.Property(e => e.TenantId).HasMaxLength(64);

        // Indexes
        entity.HasIndex(e => e.CreatedAt);
        entity.HasIndex(e => e.TenantId);
        entity.HasIndex(e => e.Name);
        entity.HasIndex(e => new { e.Name, e.TenantId });

        // (Optional) Further metadata columns can be added in future migrations (e.g., Source, Hash, UserId)
    }

    private static void ConfigureProcessDefinition(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<ProcessDefinition>();

        entity.HasKey(e => e.Id);
        entity.Property(e => e.Key).IsRequired().HasMaxLength(255);
        entity.Property(e => e.Name).IsRequired().HasMaxLength(500);
        entity.Property(e => e.BpmnXml).IsRequired();
        entity.Property(e => e.TenantId).HasMaxLength(64);

        entity.HasOne(d => d.Deployment)
            .WithMany()
            .HasForeignKey(d => d.DeploymentId)
            .OnDelete(DeleteBehavior.Cascade);

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

        entity.HasOne(e => e.ProcessDefinition)
            .WithMany()
            .HasForeignKey(e => e.ProcessDefinitionId)
            .OnDelete(DeleteBehavior.Restrict);

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
        entity.Property(e => e.State).HasMaxLength(50);
        entity.Property(e => e.AssignedWorker).HasMaxLength(255);

        entity.Property(e => e.Variables)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null!),
                v => JsonSerializer.Deserialize<Dictionary<string, object>>(v, (JsonSerializerOptions)null!) ?? new Dictionary<string, object>());

        // FK -> ProcessInstance
        entity.HasOne<ProcessInstance>()
            .WithMany()
            .HasForeignKey(e => e.ProcessInstanceId)
            .OnDelete(DeleteBehavior.Cascade);

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
        entity.Property(e => e.Name).IsRequired().HasMaxLength(255);
        entity.Property(e => e.Type).IsRequired().HasMaxLength(100);
        entity.Property(e => e.TenantId).HasMaxLength(64);

        // FK -> ProcessInstance
        entity.HasOne(v => v.ProcessInstance)
            .WithMany()
            .HasForeignKey(v => v.ProcessInstanceId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasIndex(e => e.ScopeId);
        entity.HasIndex(e => e.Name);
        entity.HasIndex(e => e.Type);
        entity.HasIndex(e => e.TenantId);
        entity.HasIndex(e => e.ProcessInstanceId);
    }

    private static void ConfigureJob(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<Job>();

        entity.HasKey(e => e.Id);
        entity.Property(e => e.Type).IsRequired().HasMaxLength(100);
        entity.Property(e => e.State).IsRequired().HasMaxLength(50);
        entity.Property(e => e.TenantId).HasMaxLength(64);
        entity.Property(e => e.ErrorMessage).HasMaxLength(4000);

        entity.HasOne(e => e.ProcessInstance)
            .WithMany()
            .HasForeignKey(e => e.ProcessInstanceId)
            .OnDelete(DeleteBehavior.Cascade);

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
        entity.Property(e => e.TenantId).HasMaxLength(64);
        entity.Property(e => e.FormKey).HasMaxLength(255);

        // FK -> ProcessInstance
        entity.HasOne<ProcessInstance>()
            .WithMany()
            .HasForeignKey(e => e.ProcessInstanceId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasIndex(e => e.ProcessInstanceId);
        entity.HasIndex(e => e.Type);
        entity.HasIndex(e => e.TenantId);
        entity.HasIndex(e => e.Assignee);
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

        entity.HasOne(e => e.ProcessInstance)
            .WithMany()
            .HasForeignKey(e => e.ProcessInstanceId)
            .OnDelete(DeleteBehavior.Cascade);

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

        entity.HasOne(e => e.ProcessInstance)
            .WithMany()
            .HasForeignKey(e => e.ProcessInstanceId)
            .OnDelete(DeleteBehavior.Cascade);

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

        // FK -> ProcessInstance
        entity.HasOne<ProcessInstance>()
            .WithMany()
            .HasForeignKey(e => e.ProcessInstanceId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasIndex(e => e.ProcessInstanceId);
        entity.HasIndex(e => e.ActivityId);
        entity.HasIndex(e => new { e.ProcessInstanceId, e.ActivityId });
    }

    private static void ConfigureUser(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<User>();
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Username).IsRequired().HasMaxLength(200);
        entity.Property(e => e.Email).IsRequired().HasMaxLength(400);
        entity.Property(e => e.IsActive).IsRequired();
        entity.Property(e => e.Roles)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null!),
                v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions)null!) ?? new List<string>());
        entity.HasIndex(e => e.Username).IsUnique(false);
        entity.HasIndex(e => e.Email);
        entity.HasIndex(e => e.IsActive);
        entity.HasIndex(e => e.CreatedAt);
    }
}
