using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VertexBPMN.Domain.Entities;

public class BpmnFlowNodeEntity
{
    [Key] public Guid Id { get; set; }
    [Required, MaxLength(255)] public string BpmnId { get; set; } = string.Empty;
    [Required, MaxLength(100)] public string Type { get; set; } = string.Empty; // startEvent, userTask, gateway...
    [MaxLength(255)] public string? SubprocessId { get; set; }
    public bool IsEvent { get; set; }
    public bool IsGateway { get; set; }
    public bool IsTask { get; set; }
    public bool IsSubprocess { get; set; }
    public bool IsEndEvent { get; set; }
    public bool IsTransactionalSubprocess { get; set; }
    public bool IsEventSubprocess { get; set; }

    // JSON extension attributes (key/value pairs)
    public string? ExtensionAttributesJson { get; set; }

    // Multi-instance metadata (flattened)
    public bool? MiIsSequential { get; set; }
    public int? MiCardinality { get; set; }
    public string? MiCollection { get; set; }
    public string? MiElementVariable { get; set; }
    public string? MiInputElement { get; set; }
    public string? MiOutputElement { get; set; }
    public string? MiCompletionCondition { get; set; }

    [ForeignKey(nameof(ProcessDefinition))]
    public Guid ProcessDefinitionId { get; set; }
    public BpmnProcessDefinitionEntity ProcessDefinition { get; set; } = null!;
}

public class BpmnSequenceFlowEntity
{
    [Key] public Guid Id { get; set; }
    [Required, MaxLength(255)] public string BpmnId { get; set; } = string.Empty;
    [Required, MaxLength(255)] public string SourceRef { get; set; } = string.Empty;
    [Required, MaxLength(255)] public string TargetRef { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public int? Priority { get; set; }
    public string? ConditionExpression { get; set; }
    public string? ExtensionAttributesJson { get; set; }
    [ForeignKey(nameof(ProcessDefinition))]
    public Guid ProcessDefinitionId { get; set; }
    public BpmnProcessDefinitionEntity ProcessDefinition { get; set; } = null!;
}

// Artifacts, Collaboration & DI simplified
public class BpmnArtifactTextAnnotationEntity
{
    [Key] public Guid Id { get; set; }
    [Required, MaxLength(255)] public string BpmnId { get; set; } = string.Empty;
    public string? Text { get; set; }
    [ForeignKey(nameof(ProcessDefinition))]
    public Guid ProcessDefinitionId { get; set; }
    public BpmnProcessDefinitionEntity ProcessDefinition { get; set; } = null!;
}

public class BpmnAssociationEntity
{
    [Key] public Guid Id { get; set; }
    [Required, MaxLength(255)] public string BpmnId { get; set; } = string.Empty;
    [MaxLength(255)] public string SourceRef { get; set; } = string.Empty;
    [MaxLength(255)] public string TargetRef { get; set; } = string.Empty;
    [MaxLength(30)] public string? Direction { get; set; }
    [ForeignKey(nameof(ProcessDefinition))]
    public Guid ProcessDefinitionId { get; set; }
    public BpmnProcessDefinitionEntity ProcessDefinition { get; set; } = null!;
}

public class BpmnGroupEntity
{
    [Key] public Guid Id { get; set; }
    [Required, MaxLength(255)] public string BpmnId { get; set; } = string.Empty;
    [MaxLength(255)] public string? CategoryValueRef { get; set; }
    [ForeignKey(nameof(ProcessDefinition))]
    public Guid ProcessDefinitionId { get; set; }
    public BpmnProcessDefinitionEntity ProcessDefinition { get; set; } = null!;
}

public class BpmnParticipantEntity
{
    [Key] public Guid Id { get; set; }
    [Required, MaxLength(255)] public string BpmnId { get; set; } = string.Empty;
    [MaxLength(255)] public string? Name { get; set; }
    [MaxLength(255)] public string? ProcessRef { get; set; }
    [ForeignKey(nameof(ProcessDefinition))]
    public Guid ProcessDefinitionId { get; set; }
    public BpmnProcessDefinitionEntity ProcessDefinition { get; set; } = null!;
}

public class BpmnMessageFlowEntity
{
    [Key] public Guid Id { get; set; }
    [Required, MaxLength(255)] public string BpmnId { get; set; } = string.Empty;
    [MaxLength(255)] public string SourceRef { get; set; } = string.Empty;
    [MaxLength(255)] public string TargetRef { get; set; } = string.Empty;
    [MaxLength(255)] public string? Name { get; set; }
    [ForeignKey(nameof(ProcessDefinition))]
    public Guid ProcessDefinitionId { get; set; }
    public BpmnProcessDefinitionEntity ProcessDefinition { get; set; } = null!;
}

public class BpmnLaneEntity
{
    [Key] public Guid Id { get; set; }
    [Required, MaxLength(255)] public string BpmnId { get; set; } = string.Empty;
    [MaxLength(255)] public string? Name { get; set; }
    // JSON list of flow node refs
    public string FlowNodeRefsJson { get; set; } = "[]";
    [ForeignKey(nameof(ProcessDefinition))]
    public Guid ProcessDefinitionId { get; set; }
    public BpmnProcessDefinitionEntity ProcessDefinition { get; set; } = null!;
}

public class BpmnDataObjectEntity
{
    [Key] public Guid Id { get; set; }
    [Required, MaxLength(255)] public string BpmnId { get; set; } = string.Empty;
    [MaxLength(500)] public string? Name { get; set; }
    [ForeignKey(nameof(ProcessDefinition))]
    public Guid ProcessDefinitionId { get; set; }
    public BpmnProcessDefinitionEntity ProcessDefinition { get; set; } = null!;
}

public class BpmnDataStoreEntity
{
    [Key] public Guid Id { get; set; }
    [Required, MaxLength(255)] public string BpmnId { get; set; } = string.Empty;
    [MaxLength(500)] public string? Name { get; set; }
    [ForeignKey(nameof(ProcessDefinition))]
    public Guid ProcessDefinitionId { get; set; }
    public BpmnProcessDefinitionEntity ProcessDefinition { get; set; } = null!;
}

public class BpmnMessageRefEntity
{
    [Key] public Guid Id { get; set; }
    [Required, MaxLength(255)] public string BpmnId { get; set; } = string.Empty;
    [MaxLength(500)] public string? Name { get; set; }
    [ForeignKey(nameof(ProcessDefinition))] public Guid ProcessDefinitionId { get; set; }
    public BpmnProcessDefinitionEntity ProcessDefinition { get; set; } = null!;
}

public class BpmnSignalRefEntity
{
    [Key] public Guid Id { get; set; }
    [Required, MaxLength(255)] public string BpmnId { get; set; } = string.Empty;
    [MaxLength(500)] public string? Name { get; set; }
    [ForeignKey(nameof(ProcessDefinition))] public Guid ProcessDefinitionId { get; set; }
    public BpmnProcessDefinitionEntity ProcessDefinition { get; set; } = null!;
}

public class BpmnErrorRefEntity
{
    [Key] public Guid Id { get; set; }
    [Required, MaxLength(255)] public string BpmnId { get; set; } = string.Empty;
    [MaxLength(500)] public string? Name { get; set; }
    [MaxLength(100)] public string? ErrorCode { get; set; }
    [ForeignKey(nameof(ProcessDefinition))] public Guid ProcessDefinitionId { get; set; }
    public BpmnProcessDefinitionEntity ProcessDefinition { get; set; } = null!;
}

public class BpmnEscalationRefEntity
{
    [Key] public Guid Id { get; set; }
    [Required, MaxLength(255)] public string BpmnId { get; set; } = string.Empty;
    [MaxLength(500)] public string? Name { get; set; }
    [MaxLength(100)] public string? EscalationCode { get; set; }
    [ForeignKey(nameof(ProcessDefinition))] public Guid ProcessDefinitionId { get; set; }
    public BpmnProcessDefinitionEntity ProcessDefinition { get; set; } = null!;
}
