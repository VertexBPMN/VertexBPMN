namespace VertexBPMN.Api.Migration;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515", Justification = "Public contract type MigrationExecution retained for exposed HTTP action signatures and controller activation (MigrationExecution).")]
public class MigrationExecution
{
    public Guid Id { get; set; }
    public Guid MigrationPlanId { get; set; }
    public string? TenantId { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public MigrationStatus Status { get; set; }
    public bool IsDryRun { get; set; }
    public int Progress { get; set; }
    public string? Error { get; set; }
    public List<MigrationStepResult> Steps { get; set; } = new();
    public List<Guid> Snapshots { get; set; } = new();
    public List<Guid> AffectedProcessInstanceIds { get; set; } = new();
}
