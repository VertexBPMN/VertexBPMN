namespace VertexBPMN.Domain.Entities;

public sealed class PollingTriggerRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string TenantId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ProcessDefinitionKey { get; set; } = string.Empty;
    public string ConnectorType { get; set; } = string.Empty; // z.B. "http" - wiederverwendet IConnectorExecutor
    public string ConnectorAttributesJson { get; set; } = "{}"; // gleiche Attribute-Struktur wie vertex:connector.* auf Service-Tasks
    public string? CredentialId { get; set; }
    public int IntervalSeconds { get; set; } = 60;
    public string CursorStateJson { get; set; } = "{}"; // z.B. { "lastSeenId": "..." } oder { "lastSeenTimestamp": "..." }
    public bool Enabled { get; set; } = true;
    public DateTime? NextDueAt { get; set; }
    public string? LockOwner { get; set; }
    public DateTime? LockedUntil { get; set; }
    public DateTime? LastPolledAt { get; set; }
    public int ConsecutiveFailures { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastModified { get; set; } = DateTime.UtcNow;
}
