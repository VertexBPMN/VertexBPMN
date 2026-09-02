using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VertexBPMN.Domain.Entities.Bpmn;

public class BpmnArtifactTextAnnotationEntity
{
    [Key] public Guid Id { get; set; }
    [Required, MaxLength(255)] public string BpmnId { get; set; } = string.Empty;
    public string? Text { get; set; }
    [ForeignKey(nameof(ProcessDefinition))]
    public Guid ProcessDefinitionId { get; set; }
    public BpmnProcessDefinitionEntity ProcessDefinition { get; set; } = null!;
}