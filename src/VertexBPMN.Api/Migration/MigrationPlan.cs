namespace VertexBPMN.Api.Migration;

public class MigrationPlan
{
    public Guid Id { get; set; }
    public string FromProcessKey { get; set; } = string.Empty;
    public string ToProcessKey { get; set; } = string.Empty;
    public MigrationOptions Options { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public TimeSpan EstimatedDuration { get; set; }
    public string Complexity { get; set; } = string.Empty;
    public List<MigrationCompatibilityIssue> CompatibilityIssues { get; set; } = new();
    public List<ActivityMappingRule> ActivityMappings { get; set; } = new();
    public int AffectedInstances { get; set; }
    public List<MigrationStep> MigrationSteps { get; set; } = new();
    public string RiskAssessment { get; set; } = string.Empty;
    public RollbackPlan RollbackPlan { get; set; } = new();
}