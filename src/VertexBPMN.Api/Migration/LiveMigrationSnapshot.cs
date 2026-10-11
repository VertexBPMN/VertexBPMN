namespace VertexBPMN.Api.Migration;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515", Justification = "Public contract type LiveMigrationSnapshot retained for exposed HTTP action signatures and controller activation (LiveMigrationSnapshot).")]
public class LiveMigrationSnapshot
{
    public Guid Id { get; set; }
    public Guid ProcessInstanceId { get; set; }
    public string? TenantId { get; set; }
    public DateTime CreatedAt { get; set; }
    public string ProcessState { get; set; } = string.Empty;
    public Dictionary<string, string> TokenStates { get; set; } = new(StringComparer.Ordinal);
    public Dictionary<string, string> Variables { get; set; } = new(StringComparer.Ordinal);
    public Dictionary<string, string> ActivityStates { get; set; } = new(StringComparer.Ordinal);
    public Dictionary<Guid, string> TaskActivityIds { get; set; } = new();
    public Dictionary<Guid, string> JobActivityIds { get; set; } = new();
    public Dictionary<Guid, string> SubscriptionActivityIds { get; set; } = new();
    public Dictionary<Guid, string?> SubscriptionActiveKeys { get; set; } = new();
    public Dictionary<Guid, string?> IncidentActivityIds { get; set; } = new();
    public Dictionary<Guid, string> MultiInstanceActivityIds { get; set; } = new();
}
