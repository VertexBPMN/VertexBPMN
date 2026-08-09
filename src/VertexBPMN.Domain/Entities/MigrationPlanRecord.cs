namespace VertexBPMN.Domain.Entities;

public class MigrationPlanRecord
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public string Payload { get; set; } = string.Empty;
}