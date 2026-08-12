using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Extensions;
using VertexBPMN.Domain.Model.Bpmn;


namespace VertexBPMN.Infrastructure.Persistence;

/// <summary>
/// Persistence projection DbContext for BpmnModel (read/query optimized).
/// NOT used for runtime token state – only structure/catalog metadata.
/// </summary>
public class BpmnDbContext : DbContext
{
    public BpmnDbContext(DbContextOptions<BpmnDbContext> options) : base(options) { }
    public DbSet<BpmnProcessDefinitionEntity> ProcessDefinitions => Set<BpmnProcessDefinitionEntity>();
    public DbSet<BpmnFlowNodeEntity> FlowNodes => Set<BpmnFlowNodeEntity>();
    public DbSet<BpmnSequenceFlowEntity> SequenceFlows => Set<BpmnSequenceFlowEntity>();
    public DbSet<BpmnArtifactTextAnnotationEntity> TextAnnotations => Set<BpmnArtifactTextAnnotationEntity>();
    public DbSet<BpmnAssociationEntity> Associations => Set<BpmnAssociationEntity>();
    public DbSet<BpmnGroupEntity> Groups => Set<BpmnGroupEntity>();
    public DbSet<BpmnParticipantEntity> Participants => Set<BpmnParticipantEntity>();
    public DbSet<BpmnMessageFlowEntity> MessageFlows => Set<BpmnMessageFlowEntity>();
    public DbSet<BpmnLaneEntity> Lanes => Set<BpmnLaneEntity>();
    public DbSet<BpmnDataObjectEntity> DataObjects => Set<BpmnDataObjectEntity>();
    public DbSet<BpmnDataStoreEntity> DataStores => Set<BpmnDataStoreEntity>();
    public DbSet<BpmnMessageRefEntity> MessageRefs => Set<BpmnMessageRefEntity>();
    public DbSet<BpmnSignalRefEntity> SignalRefs => Set<BpmnSignalRefEntity>();
    public DbSet<BpmnErrorRefEntity> ErrorRefs => Set<BpmnErrorRefEntity>();
    public DbSet<BpmnEscalationRefEntity> EscalationRefs => Set<BpmnEscalationRefEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ConfigureProcessDefinition(modelBuilder);
        ConfigureFlowNode(modelBuilder);
        ConfigureSequenceFlow(modelBuilder);
        ConfigureLane(modelBuilder);
        ConfigureArtifacts(modelBuilder);
        ConfigureReferences(modelBuilder);
    }

    private static void ConfigureProcessDefinition(ModelBuilder mb)
    {
        var e = mb.Entity<BpmnProcessDefinitionEntity>();
        e.HasIndex(x => x.ProcessId);
        e.HasIndex(x => new { x.Key, x.Version }).IsUnique();
        e.HasIndex(x => x.TenantId);
        e.Property(x => x.ContentHash).HasMaxLength(128);
        e.HasMany(x => x.FlowNodes).WithOne(x => x.ProcessDefinition).HasForeignKey(x => x.ProcessDefinitionId).OnDelete(DeleteBehavior.Cascade);
    }

    private static void ConfigureFlowNode(ModelBuilder mb)
    {
        var e = mb.Entity<BpmnFlowNodeEntity>();
        e.HasIndex(x => x.BpmnId);
        e.HasIndex(x => x.Type);
        e.Property(x => x.ExtensionAttributesJson).HasPortableJsonColumn();
    }

    private static void ConfigureSequenceFlow(ModelBuilder mb)
    {
        var e = mb.Entity<BpmnSequenceFlowEntity>();
        e.HasIndex(x => x.BpmnId).IsUnique(false);
        e.HasIndex(x => new { x.SourceRef, x.TargetRef });
        e.Property(x => x.ExtensionAttributesJson).HasPortableJsonColumn();
    }

    private static void ConfigureLane(ModelBuilder mb)
    {
        var e = mb.Entity<BpmnLaneEntity>();
        e.Property(x => x.FlowNodeRefsJson).HasPortableJsonColumn();
        e.HasIndex(x => x.BpmnId);
    }

    private static void ConfigureArtifacts(ModelBuilder mb)
    {
        mb.Entity<BpmnArtifactTextAnnotationEntity>().HasIndex(x => x.BpmnId);
        mb.Entity<BpmnAssociationEntity>().HasIndex(x => x.BpmnId);
        mb.Entity<BpmnGroupEntity>().HasIndex(x => x.BpmnId);
        mb.Entity<BpmnParticipantEntity>().HasIndex(x => x.BpmnId);
        mb.Entity<BpmnMessageFlowEntity>().HasIndex(x => x.BpmnId);
        mb.Entity<BpmnDataObjectEntity>().HasIndex(x => x.BpmnId);
        mb.Entity<BpmnDataStoreEntity>().HasIndex(x => x.BpmnId);
    }

    private static void ConfigureReferences(ModelBuilder mb)
    {
        mb.Entity<BpmnMessageRefEntity>().HasIndex(x => x.BpmnId);
        mb.Entity<BpmnSignalRefEntity>().HasIndex(x => x.BpmnId);
        mb.Entity<BpmnErrorRefEntity>().HasIndex(x => x.BpmnId);
        mb.Entity<BpmnEscalationRefEntity>().HasIndex(x => x.BpmnId);
    }

    /// <summary>
    /// Materialize an EF entity graph from a BpmnModel. Caller must set BpmnXml externally.
    /// </summary>
    public BpmnProcessDefinitionEntity MaterializeFromUnified(BpmnModel model, string key, int version, string hash, string? tenantId = null, string? name = null, string? originalXml = null)
    {
        var def = new BpmnProcessDefinitionEntity
        {
            Id = Guid.NewGuid(),
            ProcessId = model.ProcessId,
            Key = key,
            Version = version,
            Name = name,
            BpmnXml = originalXml ?? string.Empty,
            ContentHash = hash,
            TenantId = tenantId,
            CreatedAt = DateTime.UtcNow
        };

        // Events
        foreach (var e in model.Events)
        {
            def.FlowNodes.Add(new BpmnFlowNodeEntity
            {
                Id = Guid.NewGuid(),
                BpmnId = e.Id,
                Type = e.Type,
                SubprocessId = e.SubprocessId,
                IsEvent = true,
                IsEndEvent = e.Type == "endEvent",
                ProcessDefinitionId = def.Id
            });
        }
        // Tasks
        foreach (var t in model.Tasks)
        {
            def.FlowNodes.Add(new BpmnFlowNodeEntity
            {
                Id = Guid.NewGuid(),
                BpmnId = t.Id,
                Type = t.Type,
                SubprocessId = t.SubprocessId,
                IsTask = true,
                ProcessDefinitionId = def.Id
            });
        }
        // Gateways
        foreach (var g in model.Gateways)
        {
            def.FlowNodes.Add(new BpmnFlowNodeEntity
            {
                Id = Guid.NewGuid(),
                BpmnId = g.Id,
                Type = g.Type,
                SubprocessId = g.SubprocessId,
                IsGateway = true,
                ProcessDefinitionId = def.Id
            });
        }
        // Subprocesses
        foreach (var sp in model.Subprocesses)
        {
            var mi = sp.Loop as MultiInstanceLoopCharacteristics;
            def.FlowNodes.Add(new BpmnFlowNodeEntity
            {
                Id = Guid.NewGuid(),
                BpmnId = sp.Id,
                Type = "subProcess",
                SubprocessId = sp.SubprocessId,
                IsSubprocess = true,
                IsTransactionalSubprocess = sp.IsTransaction,
                IsEventSubprocess = sp.IsEventSubprocess,
                MiIsSequential = mi?.IsSequential,
                MiCardinality = mi?.LoopCardinality,
                MiCollection = mi?.Collection,
                MiElementVariable = mi?.ElementVariable,
                MiInputElement = mi?.InputElement,
                MiOutputElement = mi?.OutputElement,
                MiCompletionCondition = mi?.CompletionCondition,
                ProcessDefinitionId = def.Id
            });
        }
        // SequenceFlows
        foreach (var sf in model.SequenceFlows)
        {
            def.SequenceFlows.Add(new BpmnSequenceFlowEntity
            {
                Id = Guid.NewGuid(),
                BpmnId = sf.Id,
                SourceRef = sf.SourceRef,
                TargetRef = sf.TargetRef,
                IsDefault = sf.IsDefault,
                Priority = sf.Priority,
                ConditionExpression = sf.ConditionExpression,
                ProcessDefinitionId = def.Id
            });
        }
        // Artifacts & collaboration
        foreach (var ta in model.TextAnnotations ?? Array.Empty<BpmnTextAnnotation>())
            def.TextAnnotations.Add(new BpmnArtifactTextAnnotationEntity { Id = Guid.NewGuid(), BpmnId = ta.Id, Text = ta.Text, ProcessDefinitionId = def.Id });
        foreach (var assoc in model.Associations ?? Array.Empty<BpmnAssociation>())
            def.Associations.Add(new BpmnAssociationEntity { Id = Guid.NewGuid(), BpmnId = assoc.Id, SourceRef = assoc.SourceRef, TargetRef = assoc.TargetRef, Direction = assoc.Direction, ProcessDefinitionId = def.Id });
        foreach (var grp in model.Groups ?? Array.Empty<BpmnGroup>())
            def.Groups.Add(new BpmnGroupEntity { Id = Guid.NewGuid(), BpmnId = grp.Id, CategoryValueRef = grp.CategoryValueRef, ProcessDefinitionId = def.Id });
        foreach (var part in model.Participants ?? Array.Empty<BpmnParticipant>())
            def.Participants.Add(new BpmnParticipantEntity { Id = Guid.NewGuid(), BpmnId = part.Id, Name = part.Name, ProcessRef = part.ProcessRef, ProcessDefinitionId = def.Id });
        foreach (var mf in model.MessageFlows ?? Array.Empty<BpmnMessageFlow>())
            def.MessageFlows.Add(new BpmnMessageFlowEntity { Id = Guid.NewGuid(), BpmnId = mf.Id, SourceRef = mf.SourceRef, TargetRef = mf.TargetRef, Name = mf.Name, ProcessDefinitionId = def.Id });
        foreach (var lane in model.Lanes ?? Array.Empty<BpmnLane>())
            def.Lanes.Add(new BpmnLaneEntity { Id = Guid.NewGuid(), BpmnId = lane.Id, Name = lane.Name, FlowNodeRefsJson = JsonSerializer.Serialize(lane.FlowNodeRefs), ProcessDefinitionId = def.Id });
        // Data
        foreach (var d in model.DataObjects)
            def.DataObjects.Add(new BpmnDataObjectEntity { Id = Guid.NewGuid(), BpmnId = d.Id, Name = d.Name, ProcessDefinitionId = def.Id });
        foreach (var ds in model.DataStores)
            def.DataStores.Add(new BpmnDataStoreEntity { Id = Guid.NewGuid(), BpmnId = ds.Id, Name = ds.Name, ProcessDefinitionId = def.Id });
        // Ref catalogs
        foreach (var m in model.Messages)
            def.Messages.Add(new BpmnMessageRefEntity { Id = Guid.NewGuid(), BpmnId = m.Id, Name = m.Name, ProcessDefinitionId = def.Id });
        foreach (var s in model.Signals)
            def.Signals.Add(new BpmnSignalRefEntity { Id = Guid.NewGuid(), BpmnId = s.Id, Name = s.Name, ProcessDefinitionId = def.Id });
        foreach (var er in model.Errors)
            def.Errors.Add(new BpmnErrorRefEntity { Id = Guid.NewGuid(), BpmnId = er.Id, Name = er.Name, ErrorCode = er.ErrorCode, ProcessDefinitionId = def.Id });
        foreach (var esc in model.Escalations)
            def.Escalations.Add(new BpmnEscalationRefEntity { Id = Guid.NewGuid(), BpmnId = esc.Id, Name = esc.Name, EscalationCode = esc.EscalationCode, ProcessDefinitionId = def.Id });

        return def;
    }
}
