using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VertexBPMN.Domain.Entities.Bpmn;

public class BpmnErrorRefEntity
{
    [Key] public Guid Id { get; set; }
    [Required, MaxLength(255)] public string BpmnId { get; set; } = string.Empty;
    [MaxLength(500)] public string? Name { get; set; }
    [MaxLength(100)] public string? ErrorCode { get; set; }
    [ForeignKey(nameof(ProcessDefinition))] public Guid ProcessDefinitionId { get; set; }
    public BpmnProcessDefinitionEntity ProcessDefinition { get; set; } = null!;
}