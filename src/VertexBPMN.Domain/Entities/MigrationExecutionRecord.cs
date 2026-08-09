namespace VertexBPMN.Domain.Entities;

public class MigrationExecutionRecord
{
    public Guid Id { get; set; }
    public Guid MigrationPlanId { get; set; }
    public DateTime StartedAt { get; set; }
    public string Payload { get; set; } = string.Empty;
}