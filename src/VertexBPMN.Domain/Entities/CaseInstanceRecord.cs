namespace VertexBPMN.Domain.Entities;

public sealed class CaseInstanceRecord
{
    public Guid Id { get; set; }
    public string CaseDefinitionId { get; set; } = string.Empty;
    public string CaseDefinitionKey { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public string State { get; set; } = "Active";
    public string CaseFileJson { get; set; } = "{}";
    public string PlanItemStatesJson { get; set; } = "{}";
    public string DiscretionaryItemsJson { get; set; } = "[]";
    public DateTime CreatedAt { get; set; }
    public DateTime LastModified { get; set; }
    public DateTime? CompletedAt { get; set; }
    public long Revision { get; set; }
}
