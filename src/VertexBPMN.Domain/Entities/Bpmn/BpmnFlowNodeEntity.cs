using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VertexBPMN.Domain.Entities.Bpmn;

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
