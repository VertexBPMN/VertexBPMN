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
    public DbSet<EventSubscription> EventSubscriptions => Set<EventSubscription>();
    public DbSet<RuntimeOutboxMessage> RuntimeOutbox => Set<RuntimeOutboxMessage>();
    public DbSet<RuntimeInboxMessage> RuntimeInbox => Set<RuntimeInboxMessage>();
    public DbSet<WorkerRegistration> WorkerRegistrations => Set<WorkerRegistration>();
    public DbSet<MultiInstanceExecution> MultiInstanceExecutions => Set<MultiInstanceExecution>();
    public DbSet<EngineDeployment> EngineDeployments => Set<EngineDeployment>();
    public DbSet<User> Users => Set<User>();
    public DbSet<MigrationPlanRecord> MigrationPlans => Set<MigrationPlanRecord>();
    public DbSet<MigrationExecutionRecord> MigrationExecutions => Set<MigrationExecutionRecord>();
    public DbSet<CmmnHistoryRecord> CmmnHistory => Set<CmmnHistoryRecord>();
    public DbSet<FeatureFlagRecord> FeatureFlags => Set<FeatureFlagRecord>();
    public DbSet<IdentityGroupRecord> IdentityGroups => Set<IdentityGroupRecord>();
    public DbSet<IdentityGroupMembershipRecord> IdentityGroupMemberships => Set<IdentityGroupMembershipRecord>();
    public DbSet<IdentityAuthorizationRecord> IdentityAuthorizations => Set<IdentityAuthorizationRecord>();
    public DbSet<CredentialRecord> Credentials => Set<CredentialRecord>();
    public DbSet<ConnectorRecord> Connectors => Set<ConnectorRecord>();
    public DbSet<ConnectorTemplateRecord> ConnectorTemplates => Set<ConnectorTemplateRecord>();
    public DbSet<WorkflowTrigger> WorkflowTriggers => Set<WorkflowTrigger>();
    public DbSet<FormDefinitionRecord> FormDefinitions => Set<FormDefinitionRecord>();
    public DbSet<CaseDefinitionRecord> CaseDefinitions => Set<CaseDefinitionRecord>();
    public DbSet<CaseInstanceRecord> CaseInstances => Set<CaseInstanceRecord>();
    public DbSet<PollingTriggerRecord> PollingTriggers => Set<PollingTriggerRecord>();
    public DbSet<OAuth2FlowStateRecord> OAuth2FlowStates => Set<OAuth2FlowStateRecord>();

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
        ConfigureEventSubscriptions(modelBuilder);
        ConfigureRuntimeMessaging(modelBuilder);
        ConfigureWorkerRegistrations(modelBuilder);
        ConfigureMultiInstanceExecution(modelBuilder);
        ConfigureUser(modelBuilder);
        ConfigureMigrationRecords(modelBuilder);
        ConfigureCmmnHistory(modelBuilder);
        ConfigureFeatureFlags(modelBuilder);
        ConfigureIdentity(modelBuilder);
        ConfigureCredentials(modelBuilder);
        ConfigureConnectors(modelBuilder);
        ConfigureConnectorTemplates(modelBuilder);
        ConfigureWorkflowTriggers(modelBuilder);
        ConfigurePollingTriggers(modelBuilder);
        ConfigureOAuth2FlowStates(modelBuilder);
        ConfigureFormDefinitions(modelBuilder);
        ConfigureCaseDefinitions(modelBuilder);

        ConfigureCaseInstances(modelBuilder);
        // Only identity bootstrap data is seeded. Runtime state must exclusively be
        // created by deployments and process execution; a due sample job would be
        // indistinguishable from real work to every production worker.
        var seedTimestamp = new DateTime(2025, 01, 01, 0, 0, 0, DateTimeKind.Utc);

        // Seed initial users (align with IdentityService defaults)
        modelBuilder.Entity<User>().HasData(
            new User { Id = "1", Username = "admin", Email = "admin@example.com", IsActive = true, Roles = new List<string> { "admin" }, CreatedAt = seedTimestamp, LastModified = seedTimestamp },
            new User { Id = "2", Username = "user1", Email = "user1@example.com", IsActive = true, Roles = new List<string> { "user" }, CreatedAt = seedTimestamp, LastModified = seedTimestamp },
            new User { Id = "3", Username = "user2", Email = "user2@example.com", IsActive = true, Roles = new List<string> { "user" }, CreatedAt = seedTimestamp, LastModified = seedTimestamp }
        );

        base.OnModelCreating(modelBuilder);
    }

    private static void ConfigureConnectors(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<ConnectorRecord>();
        entity.HasKey(e => e.Id);
        entity.Property(e => e.TenantId).IsRequired().HasMaxLength(64);
        entity.Property(e => e.Name).IsRequired().HasMaxLength(256);
        entity.Property(e => e.Type).IsRequired().HasMaxLength(128);
        entity.Property(e => e.Description).HasMaxLength(2000);
        entity.Property(e => e.Endpoint).HasMaxLength(2048);
        entity.Property(e => e.CredentialId).HasMaxLength(128);
        entity.Property(e => e.TemplateId).HasMaxLength(128);
        entity.Property(e => e.Enabled).IsRequired();
        entity.HasIndex(e => new { e.TenantId, e.Name }).IsUnique();
        entity.HasIndex(e => e.TenantId);
        entity.HasIndex(e => e.CredentialId);
        entity.HasIndex(e => e.TemplateId);
        entity.HasIndex(e => e.LastModified);
    }


    private static void ConfigureConnectorTemplates(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<ConnectorTemplateRecord>();
        entity.HasKey(e => e.Id);
        entity.Property(e => e.TenantId).IsRequired().HasMaxLength(64);
        entity.Property(e => e.Name).IsRequired().HasMaxLength(256);
        entity.Property(e => e.Category).IsRequired().HasMaxLength(128);
        entity.Property(e => e.Runtime).IsRequired().HasMaxLength(128);
        entity.Property(e => e.Icon).HasMaxLength(256);
        entity.Property(e => e.AppliesToJson).IsRequired();
        entity.Property(e => e.PropertiesJson).IsRequired();
        entity.HasIndex(e => new { e.TenantId, e.Name }).IsUnique();
        entity.HasIndex(e => e.TenantId);
    }

    private static void ConfigureWorkflowTriggers(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<WorkflowTrigger>();
        entity.HasKey(trigger => trigger.Id);
        entity.Property(trigger => trigger.Name).IsRequired().HasMaxLength(256);
        entity.Property(trigger => trigger.ProcessDefinitionKey).IsRequired().HasMaxLength(255);
        entity.Property(trigger => trigger.TenantId).HasMaxLength(64);
        entity.Property(trigger => trigger.SecretHash).IsRequired().HasMaxLength(128);
        entity.Property(trigger => trigger.Path).HasMaxLength(512);
        entity.Property(trigger => trigger.Method).HasMaxLength(16);
        entity.Property(trigger => trigger.AuthenticationMode).IsRequired().HasMaxLength(32);
        entity.Property(trigger => trigger.CredentialId).HasMaxLength(128);
        entity.Property(trigger => trigger.CredentialSecretKey).HasMaxLength(128);
        entity.Property(trigger => trigger.CorrelationKey).HasMaxLength(256);
        entity.Property(trigger => trigger.Enabled).IsRequired();
        entity.Property(trigger => trigger.InvocationCount).IsRequired();
        entity.HasIndex(trigger => new { trigger.TenantId, trigger.Name }).IsUnique();
        entity.HasIndex(trigger => trigger.ProcessDefinitionKey);
        entity.HasIndex(trigger => trigger.TenantId);
        entity.HasIndex(trigger => trigger.LastModified);
        // The ingress route does not contain a tenant segment, so endpoint ownership must
        // be global; otherwise an unauthenticated request could be ambiguous.
        entity.HasIndex(trigger => new { trigger.Path, trigger.Method }).IsUnique();
        entity.HasIndex(trigger => new { trigger.TenantId, trigger.ProcessDefinitionKey, trigger.SourceElementId }).IsUnique();
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

    private static void ConfigureCmmnHistory(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CmmnHistoryRecord>(entity =>
        {
            entity.HasKey(record => record.Id);
            entity.Property(record => record.CaseId).IsRequired().HasMaxLength(255);
            entity.Property(record => record.CaseFileJson).IsRequired();
            entity.Property(record => record.CompletedPlanItemsJson).IsRequired();
            entity.HasIndex(record => new { record.CaseId, record.Timestamp });
        });
    }

    private static void ConfigureFeatureFlags(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<FeatureFlagRecord>(entity =>
        {
            entity.HasKey(record => record.Name);
            entity.Property(record => record.Name).HasMaxLength(128);
        });

        modelBuilder.Entity<FeatureFlagRecord>().HasData(
            new FeatureFlagRecord { Name = "liveinspector", Enabled = true },
            new FeatureFlagRecord { Name = "predictiveanalytics", Enabled = false },
            new FeatureFlagRecord { Name = "processminingapi", Enabled = false },
            new FeatureFlagRecord { Name = "task-io-snapshots", Enabled = false });
    }

    private static void ConfigureIdentity(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<IdentityGroupRecord>(entity =>
        {
            entity.HasKey(group => group.Id);
            entity.Property(group => group.Name).IsRequired().HasMaxLength(256);
            entity.Property(group => group.Type).IsRequired().HasMaxLength(128);
            entity.Property(group => group.TenantId).HasMaxLength(64);
            entity.HasIndex(group => new { group.TenantId, group.Name }).IsUnique();
        });

        modelBuilder.Entity<IdentityGroupMembershipRecord>(entity =>
        {
            entity.HasKey(membership => new { membership.GroupId, membership.UserId });
            entity.Property(membership => membership.GroupId).HasMaxLength(128);
            entity.Property(membership => membership.UserId).HasMaxLength(128);
            entity.Property(membership => membership.TenantId).HasMaxLength(64);
            entity.HasIndex(membership => membership.TenantId);
            entity.HasIndex(membership => membership.UserId);
        });

        modelBuilder.Entity<IdentityAuthorizationRecord>(entity =>
        {
            entity.HasKey(authorization => authorization.Id);
            entity.Property(authorization => authorization.Id).HasMaxLength(128);
            entity.Property(authorization => authorization.UserId).HasMaxLength(128);
            entity.Property(authorization => authorization.GroupId).HasMaxLength(128);
            entity.Property(authorization => authorization.Resource).IsRequired().HasMaxLength(512);
            entity.Property(authorization => authorization.Permissions).IsRequired().HasMaxLength(512);
            entity.Property(authorization => authorization.TenantId).HasMaxLength(64);
            entity.HasIndex(authorization => authorization.TenantId);
            entity.HasIndex(authorization => new { authorization.UserId, authorization.GroupId, authorization.Resource }).IsUnique();
        });
    }

    private static void ConfigureCredentials(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CredentialRecord>(entity =>
        {
            entity.HasKey(credential => credential.Id);
            entity.Property(credential => credential.Id).HasMaxLength(128);
            entity.Property(credential => credential.TenantId).IsRequired().HasMaxLength(64);
            entity.Property(credential => credential.Name).IsRequired().HasMaxLength(256);
            entity.Property(credential => credential.Type).IsRequired().HasMaxLength(128);
            entity.Property(credential => credential.Description).HasMaxLength(2000);
            entity.Property(credential => credential.SecretKeysJson).IsRequired().HasMaxLength(4000);
            entity.Property(credential => credential.ProtectedValues).IsRequired();
            entity.HasIndex(credential => new { credential.TenantId, credential.Name }).IsUnique();
            entity.HasIndex(credential => credential.TenantId);
            entity.HasIndex(credential => credential.LastModified);
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
        entity.Property(e => e.TenantScope).IsRequired().HasMaxLength(128);

        entity.HasOne(d => d.Deployment)
            .WithMany()
            .HasForeignKey(d => d.DeploymentId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasIndex(e => e.Key);
        entity.HasIndex(e => new { e.TenantScope, e.Key, e.Version }).IsUnique();
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
        entity.Property(e => e.CallingActivityId).HasMaxLength(255);
        entity.Property(e => e.Revision).IsConcurrencyToken();

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
        entity.HasIndex(e => e.ParentProcessInstanceId);
    }

    private static void ConfigureExecutionToken(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<ExecutionToken>();

        entity.HasKey(e => e.Id);
        entity.Property(e => e.CurrentNodeId).IsRequired().HasMaxLength(255);
        entity.Property(e => e.NodeType).IsRequired().HasMaxLength(100);
        entity.Property(e => e.State).HasMaxLength(50);
        entity.Property(e => e.AssignedWorker).HasMaxLength(255);
        entity.Property(e => e.Revision).IsConcurrencyToken();

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
        entity.HasIndex(e => new { e.ProcessInstanceId, e.ScopeId, e.Name }).IsUnique();
    }

    private static void ConfigureJob(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<Job>();

        entity.HasKey(e => e.Id);
        entity.Property(e => e.Type).IsRequired().HasMaxLength(100);
        entity.Property(e => e.State).IsRequired().HasMaxLength(50);
        entity.Property(e => e.TenantId).HasMaxLength(64);
        entity.Property(e => e.ErrorMessage).HasMaxLength(4000);
        entity.Property(e => e.ActivityId).IsRequired().HasMaxLength(255);
        entity.Property(e => e.LockOwner).HasMaxLength(255);
        entity.Property(e => e.Revision).IsConcurrencyToken();

        entity.HasOne(e => e.ProcessInstance)
            .WithMany()
            .HasForeignKey(e => e.ProcessInstanceId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasIndex(e => e.ProcessInstanceId);
        entity.HasIndex(e => e.Type);
        entity.HasIndex(e => e.State);
        entity.HasIndex(e => e.DueDate);
        entity.HasIndex(e => e.TenantId);
        entity.HasIndex(e => new { e.State, e.DueDate, e.LockedUntil });
    }

    private static void ConfigureUserTask(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<UserTask>();

        entity.HasKey(e => e.Id);
        entity.Property(e => e.Name).IsRequired().HasMaxLength(500);
        entity.Property(e => e.Type).IsRequired().HasMaxLength(100);
        entity.Property(e => e.TenantId).HasMaxLength(64);
        entity.Property(e => e.FormKey).HasMaxLength(255);
        entity.Property(e => e.ActivityId).IsRequired().HasMaxLength(255);
        entity.Property(e => e.Revision).IsConcurrencyToken();
        entity.Property(e => e.LocalVariables)
            .HasConversion(
                value => JsonSerializer.Serialize(value, (JsonSerializerOptions)null!),
                value => JsonSerializer.Deserialize<Dictionary<string, object>>(value, (JsonSerializerOptions)null!)
                         ?? new Dictionary<string, object>());
        entity.Property(e => e.CandidateUsers)
            .HasConversion(
                value => JsonSerializer.Serialize(value, (JsonSerializerOptions)null!),
                value => JsonSerializer.Deserialize<List<string>>(value, (JsonSerializerOptions)null!)
                         ?? new List<string>());
        entity.Property(e => e.RequiredFields)
            .HasConversion(
                value => JsonSerializer.Serialize(value, (JsonSerializerOptions)null!),
                value => JsonSerializer.Deserialize<List<string>>(value, (JsonSerializerOptions)null!)
                         ?? new List<string>());

        // FK -> ProcessInstance
        entity.HasOne<ProcessInstance>()
            .WithMany()
            .HasForeignKey(e => e.ProcessInstanceId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasIndex(e => e.ProcessInstanceId);
        entity.HasIndex(e => e.Type);
        entity.HasIndex(e => e.TenantId);
        entity.HasIndex(e => e.Assignee);
        entity.HasIndex(e => e.MultiInstanceExecutionId);
        entity.HasIndex(e => new { e.ProcessInstanceId, e.ActivityId, e.Status });
    }


    private static void ConfigurePollingTriggers(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<PollingTriggerRecord>();
        entity.HasKey(e => e.Id);
        entity.Property(e => e.TenantId).IsRequired().HasMaxLength(64);
        entity.Property(e => e.Name).IsRequired().HasMaxLength(256);
        entity.Property(e => e.ProcessDefinitionKey).IsRequired().HasMaxLength(256);
        entity.Property(e => e.ConnectorType).IsRequired().HasMaxLength(64);
        entity.Property(e => e.ConnectorAttributesJson).IsRequired();
        entity.Property(e => e.CredentialId).HasMaxLength(128);
        entity.Property(e => e.CursorStateJson).IsRequired();
        entity.Property(e => e.LockOwner).HasMaxLength(128);
        entity.HasIndex(e => new { e.TenantId, e.Enabled, e.NextDueAt });
    }

    private static void ConfigureOAuth2FlowStates(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<OAuth2FlowStateRecord>();
        entity.HasKey(e => e.State);
        entity.Property(e => e.TenantId).IsRequired().HasMaxLength(64);
        entity.Property(e => e.CredentialId).IsRequired().HasMaxLength(128);
        entity.Property(e => e.AuthorizationUrl).IsRequired().HasMaxLength(2048);
        entity.Property(e => e.TokenUrl).IsRequired().HasMaxLength(2048);
        entity.Property(e => e.ClientId).HasMaxLength(512);
        entity.Property(e => e.RedirectUri).HasMaxLength(2048);
        entity.Property(e => e.Scopes).HasMaxLength(1024);
        entity.HasIndex(e => new { e.TenantId, e.ExpiresAt });
    }

    private static void ConfigureFormDefinitions(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<FormDefinitionRecord>();
        entity.HasKey(x => x.Id); entity.Property(x => x.TenantId).HasMaxLength(64).IsRequired(); entity.Property(x => x.Key).HasMaxLength(128).IsRequired(); entity.Property(x => x.Name).HasMaxLength(256).IsRequired(); entity.Property(x => x.Schema).IsRequired(); entity.HasIndex(x => new { x.TenantId, x.Key }).IsUnique();
    }

    private static void ConfigureCaseDefinitions(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<CaseDefinitionRecord>();
        entity.HasKey(x => x.Id); entity.Property(x => x.TenantId).HasMaxLength(64).IsRequired(); entity.Property(x => x.Key).HasMaxLength(128).IsRequired(); entity.Property(x => x.Name).HasMaxLength(256).IsRequired(); entity.Property(x => x.CmmnXml).IsRequired(); entity.HasIndex(x => new { x.TenantId, x.Key }).IsUnique();
    }

    private static void ConfigureCaseInstances(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<CaseInstanceRecord>();
        entity.HasKey(x => x.Id);
        entity.Property(x => x.CaseDefinitionId).HasMaxLength(128).IsRequired();
        entity.Property(x => x.CaseDefinitionKey).HasMaxLength(128).IsRequired();
        entity.Property(x => x.TenantId).HasMaxLength(64).IsRequired();
        entity.Property(x => x.State).HasMaxLength(32).IsRequired();
        entity.Property(x => x.CaseFileJson).IsRequired();
        entity.Property(x => x.PlanItemStatesJson).IsRequired();
        entity.Property(x => x.DiscretionaryItemsJson).IsRequired();
        entity.Property(x => x.Revision).IsConcurrencyToken();
        entity.HasIndex(x => x.CaseDefinitionId);
        entity.HasIndex(x => new { x.TenantId, x.CaseDefinitionKey, x.State });
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

    private static void ConfigureEventSubscriptions(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<EventSubscription>();
        entity.HasKey(e => e.Id);
        entity.Property(e => e.ActivityId).IsRequired().HasMaxLength(255);
        entity.Property(e => e.EventType).IsRequired().HasMaxLength(32);
        entity.Property(e => e.EventName).IsRequired().HasMaxLength(255);
        entity.Property(e => e.State).IsRequired().HasMaxLength(32);
        entity.Property(e => e.ActiveKey).HasMaxLength(320);
        entity.Property(e => e.TenantId).HasMaxLength(64);
        entity.Property(e => e.Revision).IsConcurrencyToken();
        entity.HasOne<ProcessInstance>().WithMany().HasForeignKey(e => e.ProcessInstanceId).OnDelete(DeleteBehavior.Cascade);
        entity.HasOne<ExecutionToken>().WithMany().HasForeignKey(e => e.ExecutionTokenId).OnDelete(DeleteBehavior.Cascade);
        entity.HasIndex(e => new { e.EventType, e.EventName, e.State, e.TenantId });
        entity.HasIndex(e => new { e.ProcessInstanceId, e.ActivityId, e.State });
        entity.HasIndex(e => e.ActiveKey).IsUnique();
    }

    private static void ConfigureRuntimeMessaging(ModelBuilder modelBuilder)
    {
        var outbox = modelBuilder.Entity<RuntimeOutboxMessage>();
        outbox.HasKey(e => e.Id);
        outbox.Property(e => e.EventType).IsRequired().HasMaxLength(128);
        outbox.Property(e => e.Payload).IsRequired();
        outbox.Property(e => e.State).IsRequired().HasMaxLength(32);
        outbox.Property(e => e.TenantId).HasMaxLength(64);
        outbox.Property(e => e.LockOwner).HasMaxLength(255);
        outbox.Property(e => e.LastError).HasMaxLength(4000);
        outbox.HasIndex(e => new { e.State, e.OccurredAt, e.LockedUntil });
        outbox.HasIndex(e => e.ProcessInstanceId);

        var inbox = modelBuilder.Entity<RuntimeInboxMessage>();
        inbox.HasKey(e => e.Id);
        inbox.Property(e => e.IdempotencyKey).IsRequired().HasMaxLength(255);
        inbox.Property(e => e.Operation).IsRequired().HasMaxLength(128);
        inbox.Property(e => e.TenantId).HasMaxLength(64);
        inbox.Property(e => e.TenantScope).IsRequired().HasMaxLength(128);
        inbox.HasIndex(e => new { e.TenantScope, e.Operation, e.IdempotencyKey }).IsUnique();
    }

    private static void ConfigureWorkerRegistrations(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<WorkerRegistration>();
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Id).HasMaxLength(255);
        entity.Property(e => e.HostName).IsRequired().HasMaxLength(255);
        entity.Property(e => e.SupportedNodeTypes)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null!),
                v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions)null!) ?? new List<string>());
        entity.Property(e => e.Revision).IsConcurrencyToken();
        entity.HasIndex(e => e.LastHeartbeat);
    }

    private static void ConfigureIncident(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<Incident>();

        entity.HasKey(e => e.Id);
        entity.Property(e => e.Type).IsRequired().HasMaxLength(100);
        entity.Property(e => e.Message).IsRequired().HasMaxLength(4000);
        entity.Property(e => e.State).IsRequired().HasMaxLength(50);
        entity.Property(e => e.TenantId).HasMaxLength(64);
        entity.Property(e => e.ActivityId).HasMaxLength(255);

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
        entity.Property(e => e.ItemsJson).IsRequired();
        entity.Property(e => e.ElementVariable).HasMaxLength(255);
        entity.Property(e => e.CompletionCondition).HasMaxLength(2000);
        entity.Property(e => e.OutputCollection).HasMaxLength(255);
        entity.Property(e => e.State).IsRequired().HasMaxLength(32);
        entity.Property(e => e.Revision).IsConcurrencyToken();

        // FK -> ProcessInstance
        entity.HasOne<ProcessInstance>()
            .WithMany()
            .HasForeignKey(e => e.ProcessInstanceId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasIndex(e => e.ProcessInstanceId);
        entity.HasIndex(e => e.ActivityId);
        entity.HasIndex(e => new { e.ProcessInstanceId, e.ActivityId, e.State });
    }

    private static void ConfigureUser(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<User>();
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Username).IsRequired().HasMaxLength(200);
        entity.Property(e => e.Email).IsRequired().HasMaxLength(400);
        entity.Property(e => e.TenantId).HasMaxLength(64);
        entity.Property(e => e.IsActive).IsRequired();
        entity.Property(e => e.Roles)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null!),
                v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions)null!) ?? new List<string>());
        entity.HasIndex(e => e.Username).IsUnique(false);
        entity.HasIndex(e => e.Email);
        entity.HasIndex(e => e.TenantId);
        entity.HasIndex(e => e.IsActive);
        entity.HasIndex(e => e.CreatedAt);
    }
}
