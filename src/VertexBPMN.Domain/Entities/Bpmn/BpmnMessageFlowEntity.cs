using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VertexBPMN.Domain.Entities.Bpmn;

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