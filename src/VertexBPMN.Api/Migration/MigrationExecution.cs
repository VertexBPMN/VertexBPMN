namespace VertexBPMN.Api.Migration;

public class MigrationExecution
{
    public Guid Id { get; set; }
    public Guid MigrationPlanId { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public MigrationStatus Status { get; set; }
    public bool IsDryRun { get; set; }
    public int Progress { get; set; }
    public string? Error { get; set; }
    public List<MigrationStepResult> Steps { get; set; } = new();
    public List<Guid> Snapshots { get; set; } = new();
}