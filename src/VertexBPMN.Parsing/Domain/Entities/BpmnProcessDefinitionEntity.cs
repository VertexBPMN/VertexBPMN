using System.ComponentModel.DataAnnotations;

namespace VertexBPMN.Domain.Entities;

/// <summary>
/// Root aggregate for a parsed BPMN process (BpmnModel projection).
/// Stores original XML plus a hash for idempotent deployments.
/// </summary>
public class BpmnProcessDefinitionEntity
{
    [Key]
    public Guid Id { get; set; }
    [Required, MaxLength(255)] public string ProcessId { get; set; } = string.Empty; // BPMN id
    [Required, MaxLength(255)] public string Key { get; set; } = string.Empty; // Logical key / business key
    [MaxLength(500)] public string? Name { get; set; }
    public int Version { get; set; }
    [Required] public string BpmnXml { get; set; } = string.Empty;
    [Required, MaxLength(128)] public string ContentHash { get; set; } = string.Empty; // SHA256
    [MaxLength(64)] public string? TenantId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public ICollection<BpmnFlowNodeEntity> FlowNodes { get; set; } = new List<BpmnFlowNodeEntity>();
    public ICollection<BpmnSequenceFlowEntity> SequenceFlows { get; set; } = new List<BpmnSequenceFlowEntity>();
    public ICollection<BpmnArtifactTextAnnotationEntity> TextAnnotations { get; set; } = new List<BpmnArtifactTextAnnotationEntity>();
    public ICollection<BpmnAssociationEntity> Associations { get; set; } = new List<BpmnAssociationEntity>();
    public ICollection<BpmnGroupEntity> Groups { get; set; } = new List<BpmnGroupEntity>();
    public ICollection<BpmnParticipantEntity> Participants { get; set; } = new List<BpmnParticipantEntity>();
    public ICollection<BpmnMessageFlowEntity> MessageFlows { get; set; } = new List<BpmnMessageFlowEntity>();
    public ICollection<BpmnLaneEntity> Lanes { get; set; } = new List<BpmnLaneEntity>();
    public ICollection<BpmnDataObjectEntity> DataObjects { get; set; } = new List<BpmnDataObjectEntity>();
    public ICollection<BpmnDataStoreEntity> DataStores { get; set; } = new List<BpmnDataStoreEntity>();
    public ICollection<BpmnMessageRefEntity> Messages { get; set; } = new List<BpmnMessageRefEntity>();
    public ICollection<BpmnSignalRefEntity> Signals { get; set; } = new List<BpmnSignalRefEntity>();
    public ICollection<BpmnErrorRefEntity> Errors { get; set; } = new List<BpmnErrorRefEntity>();
    public ICollection<BpmnEscalationRefEntity> Escalations { get; set; } = new List<BpmnEscalationRefEntity>();
}