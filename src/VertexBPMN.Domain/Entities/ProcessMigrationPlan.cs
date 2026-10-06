namespace VertexBPMN.Domain.Entities;

public class ProcessMigrationPlan
{
	public string SourceProcessDefinitionId { get; set; } = string.Empty;
	public string TargetProcessDefinitionId { get; set; } = string.Empty;
	public Guid? QualifiedPlanId { get; set; }
	public Dictionary<string, string> ActivityMappings { get; set; } = new(StringComparer.Ordinal); // oldActivityId -> newActivityId
}
