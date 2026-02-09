using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VertexBPMN.Domain.Entities.Bpmn;

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