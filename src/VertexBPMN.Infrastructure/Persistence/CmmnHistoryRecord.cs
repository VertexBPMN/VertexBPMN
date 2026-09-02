namespace VertexBPMN.Infrastructure.Persistence;

public sealed class CmmnHistoryRecord
{
    public Guid Id { get; set; }
    public string CaseId { get; set; } = string.Empty;
    public string CaseFileJson { get; set; } = "{}";
    public string CompletedPlanItemsJson { get; set; } = "[]";
    public DateTime Timestamp { get; set; }
}