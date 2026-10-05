namespace VertexBPMN.Domain.Entities;

public sealed class SourceControlBindingRecord
{
    public Guid Id { get; set; }
    public string TenantId { get; set; } = "";
    public long Revision { get; set; }
    public string BindingJson { get; set; } = "";
    public string GrantsJson { get; set; } = "[]";
}

public sealed class SourceControlSessionRecord
{
    public Guid Id { get; set; }
    public Guid RepositoryId { get; set; }
    public string TenantId { get; set; } = "";
    public string ActorId { get; set; } = "";
    public Guid DocumentGeneration { get; set; }
    public long Revision { get; set; }
    public string BaseCommit { get; set; } = "";
    public long ExpiresUtcTicks { get; set; }
    public string ProtectedSnapshots { get; set; } = "";
}

/// <summary>Durable idempotency receipt; never delete merely because job details expire.</summary>
public sealed class SourceControlOperationRecord
{
    public Guid Id { get; set; }
    public Guid RepositoryId { get; set; }
    public string TenantId { get; set; } = "";
    public string ActorId { get; set; } = "";
    public int Kind { get; set; }
    public string IdempotencyKey { get; set; } = "";
    public string RequestHash { get; set; } = "";
    public string ProtectedRequest { get; set; } = "";
    public int State { get; set; }
    public long UpdatedUtcTicks { get; set; }
    public long Fence { get; set; }
    public string? LeaseOwner { get; set; }
    public long? LeaseUntilUtcTicks { get; set; }
    public int? ErrorCode { get; set; }
    public string? ProtectedResult { get; set; }
}
